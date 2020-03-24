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
        InterpreterMode CurrentMode { get; set; }
        IList<string> CurrentFunction { get; set; }
        void ExecuteCommands(IEnumerable<string> commands);
    }
    
    public class Memory
    {
        public readonly Dictionary<string, int> Variables = new Dictionary<string, int>();
        public readonly Dictionary<string, IList<string>> Functions = new Dictionary<string, IList<string>>();
        public readonly List<string> Code = new List<string>();

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
            return string.Join(' ', words);
        }
    }
    public class CommandList : KeyedCollection<string, Command>
    {
        protected override string GetKeyForItem(Command item) => item.Name;
    }
    
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
            Interpreter.CurrentFunction = new List<string>();
            Memory.Functions.Add(name, Interpreter.CurrentFunction);
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
            Interpreter.ExecuteCommands(Memory.Functions[name]);
        }
    }
    
    public class SetCode : Command
    {
        public SetCode(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args) => 
            Interpreter.CurrentMode = InterpreterMode.WaitingForCode;
    }
    
    public class EndSetCode : Command
    {
        public EndSetCode(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args) => 
            Interpreter.CurrentMode = InterpreterMode.Normal;
    }
    
    public class Run : Command
    {
        public Run(IInterpreterCommandInterface interpreter) : base(interpreter) { }

        public override void Invoke(string[] args)
        {
            
        }
    }

    public class Parser
    {
        private readonly Regex _commandRegex;
        
        public Parser(CommandList possibleCommands)
        {
            var names = possibleCommands.Select(c => c.Name).OrderByDescending(n => n.Length);
            _commandRegex = new Regex($@"^(?<name>{string.Join('|', names)})(?:\s(?<args>\S+))*");
        }
        
        public (string, string[]) GetCommandAndArgs(string line)
        {
            var match = _commandRegex.Match(line);
            if (!match.Success)
                throw new ArgumentException("Команда не распознана");
            var commandName = match.Groups["name"].Value;
            var args = match.Groups["args"].Captures.Select(c => c.Value).ToArray();
            return (commandName, args);
        }
        
        public string ReadName(string input)
        {
            if (!Regex.IsMatch(input, @"^[a-zA-Z]+$"))
                throw new ArgumentException("Название переменной должно состоять из букв английского" +
                                            "алфавита");
            return input;
        }

        public int ReadValue(string input)
        {
            if (!int.TryParse(input, out var result) || result <= 0)
                throw new ArgumentException("Значение должно быть натуральным числом");
            return result;
        }
    }

    public enum InterpreterMode
    {
        Normal,
        WaitingForCode,
        ParsingFunction
    }

    public class Interpreter : IInterpreterCommandInterface
    {
        private readonly TextWriter writer;
        private IList<string> currentFunction;
        private CommandList commands;
        private readonly Memory memory = new Memory();
        private Parser parser;
        private InterpreterMode currentMode = InterpreterMode.Normal;
        private readonly string indent = new string(' ', 4);
        TextWriter IInterpreterCommandInterface.Writer => writer;
        Memory IInterpreterCommandInterface.Memory => memory;
        Parser IInterpreterCommandInterface.Parser => parser;
        

        InterpreterMode IInterpreterCommandInterface.CurrentMode
        {
            get => currentMode;
            set => currentMode = value;
        }
        IList<string> IInterpreterCommandInterface.CurrentFunction
        {
            get => currentFunction;
            set => currentFunction = value;
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

        void IInterpreterCommandInterface.ExecuteCommands(IEnumerable<string> lines)
        {
            foreach (var line in lines)
                ExecuteRegularCommand(line);
        }

        private void ExecuteRegularCommand(string line)
        {
            ExecuteCommand(line, commands);
        }

        private void TryExecuteLine(string line)
        {
            if (!(currentFunction is null))
                ProcessFunctionLine(line);
            else
                ExecuteRegularCommand(line);
        }

        private void ExecuteCommand(string line, CommandList source)
        {
            var (commandName, args) = parser.GetCommandAndArgs(line);
            var command = GetCommand(commandName, source);
            command.Invoke(args);
        }

        private void ProcessFunctionLine(string line)
        {
            if (line.StartsWith(indent))
                currentFunction.Add(line.Substring(indent.Length));
            else
            {
                currentFunction = null;
                ExecuteRegularCommand(line);
            }
        }
        
        private static Command GetCommand(string commandName,
            CommandList source)
        {
            if (!source.Contains(commandName))
                throw new ArgumentException("Команда не содержится в списке допустимых команд");
            return source[commandName];
        }
    }
}