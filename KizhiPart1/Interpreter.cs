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
        Dictionary<string, int> Variables { get; }
        void CheckIfSet(string name);
    }
    
    public abstract class Command
    {
        public readonly string Name;
        public readonly IInterpreterCommandInterface Interpreter;
        public abstract void Invoke(string[] args);

        protected Command(IInterpreterCommandInterface interpreter)
        {
            Interpreter = interpreter;
            Name = GetType().Name.ToLower();
        }
    }
    
    public class CommandList : KeyedCollection<string, Command>
    {
        protected override string GetKeyForItem(Command item) => item.Name;
    }

    public class Set : Command
    {
        public override void Invoke(string[] args)
        {
            var variable = Parsing.ReadVariableName(args[0]);
            var value = Parsing.ReadValue(args[1]);
            Interpreter.Variables[variable] = value;
        }

        public Set(IInterpreterCommandInterface interpreter) : base(interpreter) { }
    }
    
    public class Sub : Command
    {
        public override void Invoke(string[] args)
        {
            var variable = Parsing.ReadVariableName(args[0]);
            Interpreter.CheckIfSet(variable);
            var value = Parsing.ReadValue(args[1]);
            Interpreter.Variables[variable] -= value;
        }

        public Sub(IInterpreterCommandInterface interpreter) : base(interpreter) { }
    }
    
    public class Print : Command
    {
        public override void Invoke(string[] args)
        {
            var variable = Parsing.ReadVariableName(args[0]);
            Interpreter.CheckIfSet(variable);
            var value = Interpreter.Variables[variable];
            Interpreter.Writer.WriteLine(value);
        }

        public Print(IInterpreterCommandInterface interpreter) : base(interpreter) { }
    }
    
    public class Rem : Command
    {
        public override void Invoke(string[] args)
        {
            var variable = Parsing.ReadVariableName(args[0]);
            Interpreter.CheckIfSet(variable);
            Interpreter.Variables.Remove(variable);
        }

        public Rem(IInterpreterCommandInterface interpreter) : base(interpreter) { }
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
        private readonly Dictionary<string, int> _variables = new Dictionary<string, int>();
        private CommandList _commands;
        TextWriter IInterpreterCommandInterface.Writer => _writer;
        Dictionary<string, int> IInterpreterCommandInterface.Variables => _variables;

        public Interpreter(TextWriter writer)
        {
            _writer = writer;
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            _commands = new CommandList
            {
                new Set(this),
                new Sub(this),
                new Print(this),
                new Rem(this)
            };
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

        void IInterpreterCommandInterface.CheckIfSet(string variable)
        {
            if (!_variables.ContainsKey(variable))
                throw new ArgumentException("Переменная отсутствует в памяти");
        }
    }
}