using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace KizhiPart1
{   
    public class Interpreter
    {
        private readonly TextWriter _writer;
        private readonly Dictionary<string, int> _variables = new Dictionary<string, int>();
        private Dictionary<string, Action<string[]>> _commands;

        public Interpreter(TextWriter writer)
        {
            _writer = writer;
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Action<string[]>>
            {
                ["set"] = Set,
                ["sub"] = Sub,
                ["print"] = Print,
                ["rem"] = Rem
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

        private void TryExecuteLine(string command)
        {
            var tokens = command.Split(' ');
            if (tokens.Length < 1)
                throw new ArgumentException("Нужно указать название команды");
            var commandName = tokens[0];
            var args = tokens.Skip(1).ToArray();

            var action = GetCommandAction(commandName);
            action.Invoke(args);
        }

        private void Set(string[] args)
        {
            var variable = ReadVariableName(args[0]);
            var value = ReadValue(args[1]);
            _variables[variable] = value;
        }

        private void Sub(string[] args)
        {
            var variable = ReadVariableName(args[0]);
            CheckIfSet(variable);
            var value = ReadValue(args[1]);
            _variables[variable] -= value;
        }

        private void Print(string[] args)
        {
            var variable = ReadVariableName(args[0]);
            CheckIfSet(variable);
            _writer.WriteLine(_variables[variable]);
        }

        private void Rem(string[] args)
        {
            var variable = ReadVariableName(args[0]);
            CheckIfSet(variable);
            _variables.Remove(variable);
        }

        private Action<string[]> GetCommandAction(string commandName)
        {
            if (!_commands.TryGetValue(commandName, out var result))
                throw new ArgumentException("Команда не содержится в списке допустимых команд");
            return result;
        }

        private static string ReadVariableName(string arg)
        {
            if (!Regex.IsMatch(arg, @"^[a-zA-Z]+$"))
                throw new ArgumentException("Название переменной должно состоять из букв английского" +
                                            "алфавита");
            return arg;
        }

        private static int ReadValue(string arg)
        {
            if (!int.TryParse(arg, out var result) || result <= 0)
                throw new ArgumentException("Значение должно быть натуральным числом");
            return result;
        }

        private void CheckIfSet(string variable)
        {
            if (!_variables.ContainsKey(variable))
                throw new ArgumentException("Переменная отсутствует в памяти");
        }
    }
}