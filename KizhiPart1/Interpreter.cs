using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace KizhiPart1
{
    public interface IInterpreterCommandInterface
    {
        TextWriter Writer { get; }
    }

    public class Memory
    {
        public readonly Dictionary<string, int> Variables = new Dictionary<string, int>();

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
        protected readonly Memory Memory;
        public abstract void Invoke(string[] args);

        protected Command(IInterpreterCommandInterface interpreter, Memory memory)
        {
            Interpreter = interpreter;
            Memory = memory;
            Name = GetType().Name.ToLower();
        }
    }
    
    public class CommandList : KeyedCollection<string, Command>
    {
        protected override string GetKeyForItem(Command item) => item.Name;
    }

    public class Set : Command
    {
        public Set(IInterpreterCommandInterface interpreter, Memory memory) : base(interpreter, memory) { }

        public override void Invoke(string[] args)
        {
            var variable = Parsing.ReadVariableName(args[0]);
            var value = Parsing.ReadValue(args[1]);
            Memory.Variables[variable] = value;
        }
    }
    
    public class Sub : Command
    {
        public Sub(IInterpreterCommandInterface interpreter, Memory memory) : base(interpreter, memory) { }

        public override void Invoke(string[] args)
        {
            var variable = Parsing.ReadVariableName(args[0]);
            Memory.CheckIfSet(variable);
            var value = Parsing.ReadValue(args[1]);
            Memory.Variables[variable] -= value;
        }
    }
    
    public class Print : Command
    {
        public Print(IInterpreterCommandInterface interpreter, Memory memory) : base(interpreter, memory) { }

        public override void Invoke(string[] args)
        {
            var variable = Parsing.ReadVariableName(args[0]);
            Memory.CheckIfSet(variable);
            var value = Memory.Variables[variable];
            Interpreter.Writer.WriteLine(value);
        }
    }
    
    public class Rem : Command
    {
        public Rem(IInterpreterCommandInterface interpreter, Memory memory) : base(interpreter, memory) { }

        public override void Invoke(string[] args)
        {
            var variable = Parsing.ReadVariableName(args[0]);
            Memory.CheckIfSet(variable);
            Memory.Variables.Remove(variable);
        }
    }

    public static class Parsing
    {
        public static (string, string[]) GetCommandAndArgs(string line)
        {
            var tokens = line.Split(' ');
            if (tokens.Length < 1)
                throw new ArgumentException("Нужно указать название команды");
            var commandName = tokens[0];
            var args = tokens.Skip(1).ToArray();
            return (commandName, args);
        }
        
        public static string ReadVariableName(string input)
        {
            if (!Regex.IsMatch(input, @"^[a-zA-Z]+$"))
                throw new ArgumentException("Название переменной должно состоять из букв английского" +
                                            "алфавита");
            return input;
        }

        public static int ReadValue(string input)
        {
            if (!int.TryParse(input, out var result) || result <= 0)
                throw new ArgumentException("Значение должно быть натуральным числом");
            return result;
        }
    }

    public class Interpreter : IInterpreterCommandInterface
    {
        private readonly TextWriter _writer;
        private readonly CommandList _commands;
        private readonly Memory _memory = new Memory();
        TextWriter IInterpreterCommandInterface.Writer => _writer;

        public Interpreter(TextWriter writer)
        {
            _writer = writer;
            _commands = new CommandList();
            foreach (var command in FindCommands()) 
                _commands.Add(command);
        }

        private IEnumerable<Command> FindCommands()
        {
            return typeof(Command).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Command)) && !t.IsAbstract)
                .Select(t => (Command) Activator.CreateInstance(t, this, _memory));
        }

        public void ExecuteLine(string command)
        {
            try
            {
                TryExecuteLine(command);
            }
            catch (ArgumentException e)
            {
                _writer.WriteLine(e.Message);
            }
        }

        private void TryExecuteLine(string line)
        {
            var (commandName, args) = Parsing.GetCommandAndArgs(line);
            var command = GetCommand(commandName, _commands);
            command.Invoke(args);
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