using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace KizhiPart3
{
    public interface IInterpreterCommandInterface
    {
        TextWriter Writer { get; }
        Memory Memory { get; }
        Parser Parser { get; }
        InterpreterState CurrentState { get; set; }
        Stack<ExecutionContext> Stack { get; }
        void Run(RunOption option);
    }
    
    public class Memory
    {
        public readonly Dictionary<string, int> Variables = new Dictionary<string, int>();
        public readonly Dictionary<string, int> VariableLastChangePosition = new Dictionary<string, int>();
        public readonly Dictionary<string, ExecutionContext> Functions = new Dictionary<string, ExecutionContext>();
        public readonly HashSet<int> Breakpoints = new HashSet<int>();
        public ExecutionContext Code { get; set; }
        public ExecutionContext CurrentFunction { get; set; }

        public void CheckIfSet(string name)
        {
            if (!Variables.ContainsKey(name))
                throw new ArgumentException("Переменная отсутствует в памяти");
        }
    }
    
    public abstract class Command
    {
        public readonly string Name;
        protected readonly IInterpreterCommandInterface Interpreter;
        protected Memory Memory => Interpreter.Memory;
        protected Parser Parser => Interpreter.Parser;
        protected TextWriter Writer => Interpreter.Writer;
        public abstract void Invoke(string[] args);

        protected Command(IInterpreterCommandInterface interpreter)
        {
            Interpreter = interpreter;
            Name = GetName();
        }

        private string GetName()
        {
            //class CommandName => "command name"
            var className = GetType().Name;
            var words = Regex.Split(className, @"(?<!^)(?=[A-Z])").Select(s => s.ToLowerInvariant());
            return string.Join(" ", words);
        }
    }
    public class CommandList : KeyedCollection<string, Command>
    {
        protected override string GetKeyForItem(Command item) => item.Name;
    }
    
    #region Commands
    public class Set : Command
    {
        public Set(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            var variable = Parser.ReadName(args[0]);
            var value = Parser.ReadValue(args[1]);
            Memory.Variables[variable] = value;
            Memory.VariableLastChangePosition[variable] = Interpreter.Stack.Peek().AbsolutePosition;
        }
    }
    
    public class Sub : Command
    {
        public Sub(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            var variable = Parser.ReadName(args[0]);
            Memory.CheckIfSet(variable);
            var value = Parser.ReadValue(args[1]);
            Memory.Variables[variable] -= value;
            Memory.VariableLastChangePosition[variable] = Interpreter.Stack.Peek().AbsolutePosition;
        }
    }
    
    public class Print : Command
    {
        public Print(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            var variable = Parser.ReadName(args[0]);
            Memory.CheckIfSet(variable);
            var value = Memory.Variables[variable];
            Writer.WriteLine(value);
        }
    }
    
    public class Rem : Command
    {
        public Rem(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            var variable = Parser.ReadName(args[0]);
            Memory.CheckIfSet(variable);
            Memory.Variables.Remove(variable);
            Memory.VariableLastChangePosition.Remove(variable);
        }
    }
    
    public class Def : Command
    {
        public Def(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            var context = Interpreter.Stack.Peek();
            do
            {
                context.InstructionPointer++;
            } while (!context.EndReached && context.CurrentStatement.StartsWith(Parser.Indent));

            context.InstructionPointer--;
        }
    }
    
    public class Call : Command
    {
        public Call(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            var name = Parser.ReadName(args[0]);
            if (!Memory.Functions.ContainsKey(name))
                throw new ArgumentException($"Функция {name} не определена");
            var function = Memory.Functions[name];
            Interpreter.Stack.Push(function);
        }
    }
    
    public class SetCode : Command
    {
        public SetCode(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args) => 
            Interpreter.CurrentState = InterpreterState.WaitingForCode;
    }
    
    public class EndSetCode : Command
    {
        public EndSetCode(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args) =>
            Interpreter.CurrentState = InterpreterState.CodeAcquired;
    }
    
    public class Run : Command
    {
        public Run(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            if (Interpreter.Stack.Count == 0) 
                Interpreter.Stack.Push(Memory.Code);

            Interpreter.Run(RunOption.Run);
        }
    }
    
    public class AddBreak : Command
    {
        public AddBreak(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            var line = Parser.ReadValue(args[0]);
            Memory.Breakpoints.Add(line);
        }
    }
    
    public class StepOver : Command
    {
        public StepOver(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            if (Interpreter.Stack.Count == 0) 
                Interpreter.Stack.Push(Memory.Code);
            Interpreter.Run(RunOption.StepOver);
        }
    }
    
    public class Step : Command
    {
        public Step(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            if (Interpreter.Stack.Count == 0) 
                Interpreter.Stack.Push(Memory.Code);
            Interpreter.Run(RunOption.Step);
        }
    }
    
    public class PrintMem : Command
    {
        public PrintMem(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            foreach (var variable in Memory.Variables.Keys)
                Writer.WriteLine($"{variable} {Memory.Variables[variable]} " +
                                 $"{Memory.VariableLastChangePosition[variable]}");
        }
    }
    
    public class PrintTrace : Command
    {
        public PrintTrace(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            var stack = Interpreter.Stack.ToArray();
            for (var i = 0; i < stack.Length - 1; i++)
                Writer.WriteLine($"{stack[i + 1].AbsolutePosition - 1} {stack[i].Name}");
        }
    }

    #endregion

    public class Parser
    {
        private readonly CommandList commands;
        private readonly Regex commandRegex;
        public static readonly string Indent = new string(' ', 4);

        public Parser(CommandList commands)
        {
            this.commands = commands;
            var names = commands.Select(c => c.Name).OrderByDescending(n => n.Length);
            commandRegex = new Regex($@"^(?<name>{string.Join("|", names)})(?:\s(?<args>\S+))*");
        }
        
        public (string, string[]) GetCommandAndArgs(string line)
        {
            var match = commandRegex.Match(line);
            if (!match.Success)
                throw new ArgumentException("Команда не распознана");
            var commandName = match.Groups["name"].Value;
            var captures = match.Groups["args"].Captures;
            
            var args = new List<string>();
            foreach (var capture in captures) 
                args.Add(((Capture) capture).Value);
            
            return (commandName, args.ToArray());
        }
        
        public string ReadName(string input)
        {
            if (!Regex.IsMatch(input, @"^[a-zA-Z]+$"))
                throw new ArgumentException("Название переменной должно состоять из букв английского алфавита");
            return input;
        }

        public int ReadValue(string input)
        {
            if (!int.TryParse(input, out var result) || result <= 0)
                throw new ArgumentException("Значение должно быть натуральным числом");
            return result;
        }

        public bool TryDedent(string line, out string dedented)
        {
            dedented = null;
            if (!line.StartsWith(Indent))
                return false;
            dedented = line.Substring(Indent.Length);
            return true;
        }

        public void FindFunctions(IList<string> code, Memory memory, int lineOffset)
        {
            for (var i = 0; i < code.Count; i++)
            {
                var line = code[i];
                if (memory.CurrentFunction is null)
                {
                    var (command, args) = GetCommandAndArgs(line);
                    if (command == "def")
                    {
                        var name = ReadName(args[0]);
                        if (memory.Functions.ContainsKey(name))
                            throw new ArgumentException($"Функция {name} уже определена");
                        memory.CurrentFunction = new ExecutionContext(name, new List<string>(), i + 1);
                        memory.Functions.Add(name, memory.CurrentFunction);
                    }

                    continue;
                }
                if (TryDedent(line, out var dedentedLine))
                    memory.CurrentFunction.Code.Add(dedentedLine);
                else
                    memory.CurrentFunction = null;
            }
        }
    }

    public class ExecutionContext
    {
        public readonly string Name;
        public readonly IList<string> Code;
        public readonly int LineOffset;
        public int InstructionPointer { get; set; }
        public int AbsolutePosition => LineOffset + InstructionPointer;
        public string CurrentStatement => Code[InstructionPointer];
        public bool EndReached => InstructionPointer == Code.Count;

        public ExecutionContext(string name, IList<string> code, int lineOffset)
        {
            Name = name;
            Code = code;
            LineOffset = lineOffset;
        }
    }

    public enum InterpreterState
    {
        NoCode,
        WaitingForCode,
        CodeAcquired
    }

    public enum RunOption
    {
        Run,
        Step,
        StepOver
    }

    public class Debugger : IInterpreterCommandInterface
    {
        private readonly TextWriter writer;
        private CommandList commands;
        private readonly Memory memory = new Memory();
        private Parser parser;
        private InterpreterState currentState = InterpreterState.NoCode;
        private readonly Stack<ExecutionContext> stack = new Stack<ExecutionContext>();
        private int statementsExecuted;
        TextWriter IInterpreterCommandInterface.Writer => writer;
        Memory IInterpreterCommandInterface.Memory => memory;
        Parser IInterpreterCommandInterface.Parser => parser;
        Stack<ExecutionContext> IInterpreterCommandInterface.Stack => stack;

        InterpreterState IInterpreterCommandInterface.CurrentState
        {
            get => currentState;
            set => currentState = value;
        }

        public Debugger(TextWriter writer)
        {
            this.writer = writer;
            commands = new CommandList();
            foreach (var command in FindCommands())
                commands.Add(command);
            parser = new Parser(commands);
        }

        private IEnumerable<Command> FindCommands()
        {
            return typeof(Command).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Command)) && !t.IsAbstract)
                .Select(t => (Command) Activator.CreateInstance(t, this));
        }

        public void ExecuteLine(string command)
        {
            try
            {
                TryExecuteLine(command);
            }
            catch (ArgumentException e)
            {
                writer.WriteLine(e.Message);
            }
        }
        
        private void TryExecuteLine(string line)
        {
            switch (currentState)
            {
                case InterpreterState.NoCode:
                case InterpreterState.CodeAcquired:
                    ExecuteStatement(line);
                    statementsExecuted++;
                    break;
                case InterpreterState.WaitingForCode:
                    var input = line.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                    parser.FindFunctions(input, memory, statementsExecuted);
                    memory.Code = new ExecutionContext("main", input, 0);
                    currentState = InterpreterState.CodeAcquired;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void IInterpreterCommandInterface.Run(RunOption option)
        {
            var statementsExecuted = 0;
            var startingStackDepth = stack.Count;
            while (stack.Count > 0)
            {
                var context = stack.Peek();
                if (context.EndReached)
                {
                    context.InstructionPointer = 0;
                    stack.Pop();
                    if (option == RunOption.StepOver && stack.Count == startingStackDepth)
                        break;
                    if (stack.Count == 0)
                        memory.Variables.Clear();
                    continue;
                }
                
                var statement = context.CurrentStatement;
                ExecuteStatement(statement);
                if (option != RunOption.StepOver || stack.Count == startingStackDepth)
                    statementsExecuted++;
                context.InstructionPointer++;
                if (option != RunOption.Run && statementsExecuted > 0 
                    || memory.Breakpoints.Contains(context.AbsolutePosition) 
                    && (option != RunOption.StepOver || stack.Count == startingStackDepth))
                    break;
            }
        }

        private void ExecuteStatement(string statement)
        {
            var (commandName, args) = parser.GetCommandAndArgs(statement);
            var command = GetCommand(commandName, commands);
            command.Invoke(args);
        }
        
        private static Command GetCommand(string commandName, CommandList source)
        {
            if (!source.Contains(commandName))
                throw new ArgumentException("Команда не содержится в списке допустимых команд");
            return source[commandName];
        }
    }
}