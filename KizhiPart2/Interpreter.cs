using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace KizhiPart2
{
    public interface IInterpreterCommandInterface
    {
        TextWriter Writer { get; }
        Memory Memory { get; }
        Parser Parser { get; }
        InterpreterState CurrentState { get; set; }
        Stack<ExecutionContext> Stack { get; }
    }
    
    public class Memory
    {
        public readonly Dictionary<string, int> Variables = new Dictionary<string, int>();
        public readonly Dictionary<string, IList<string>> Functions = new Dictionary<string, IList<string>>();
        public IList<string> Code { get; set; }
        public IList<string> CurrentFunction { get; set; }

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
        }
    }
    
    public class Def : Command
    {
        public Def(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            var name = Parser.ReadName(args[0]);
            if (Memory.Functions.ContainsKey(name))
                throw new ArgumentException($"Функция {name} уже определена");
            Memory.CurrentFunction = new List<string>();
            Memory.Functions.Add(name, Memory.CurrentFunction);
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
            var code = Memory.Functions[name];
            var newContext = new ExecutionContext(name, code);
            Interpreter.Stack.Push(newContext);
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

        public override void Invoke(string[] args) => 
            Interpreter.Stack.Push(new ExecutionContext("Main", Memory.Code));
    }
    #endregion

    public class Parser
    {
        private readonly CommandList commands;
        private readonly Regex commandRegex;
        private static readonly string Indent = new string(' ', 4);

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

        public void FindFunctions(IList<string> code, Memory memory, out IList<string> remainingCode)
        {
            remainingCode = new List<string>();
            foreach (var line in code)
            {
                if (memory.CurrentFunction is null)
                {
                    var (command, args) = GetCommandAndArgs(line);
                    if (command == "def") 
                        commands["def"].Invoke(args);
                    else
                        remainingCode.Add(line);
                    continue;
                }
                if (TryDedent(line, out var dedentedLine))
                    memory.CurrentFunction.Add(dedentedLine);
                else
                {
                    memory.CurrentFunction = null;
                    remainingCode.Add(line);
                }
            }
        }
    }

    public class ExecutionContext
    {
        public readonly string Name;
        public readonly IList<string> Code;
        public int InstructionPointer { get; set; }

        public ExecutionContext(string name, IList<string> code)
        {
            Name = name;
            Code = code;
        }
    }

    public enum InterpreterState
    {
        NoCode,
        WaitingForCode,
        CodeAcquired
    }

    public class Interpreter : IInterpreterCommandInterface
    {
        private readonly TextWriter writer;
        private CommandList commands;
        private readonly Memory memory = new Memory();
        private Parser parser;
        private InterpreterState currentState = InterpreterState.NoCode;
        private readonly Stack<ExecutionContext> stack = new Stack<ExecutionContext>(); 
        TextWriter IInterpreterCommandInterface.Writer => writer;
        Memory IInterpreterCommandInterface.Memory => memory;
        Parser IInterpreterCommandInterface.Parser => parser;
        Stack<ExecutionContext> IInterpreterCommandInterface.Stack => stack;

        InterpreterState IInterpreterCommandInterface.CurrentState
        {
            get => currentState;
            set => currentState = value;
        }

        public Interpreter(TextWriter writer)
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
                    break;
                case InterpreterState.WaitingForCode:
                    var input = line.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                    parser.FindFunctions(input, memory, out var code);
                    memory.Code = code;
                    currentState = InterpreterState.CodeAcquired;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            ExecutePendingCode();
        }

        private void ExecutePendingCode()
        {
            while (stack.Count > 0)
            {
                var context = stack.Peek();
                if (context.InstructionPointer == context.Code.Count)
                {
                    stack.Pop();
                    if (stack.Count == 0)
                        memory.Variables.Clear();
                    continue;
                }
                
                var statement = context.Code[context.InstructionPointer];
                ExecuteStatement(statement);
                context.InstructionPointer++;
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