using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace KizhiPart2
{
    public enum Mode
    {
        Normal,
        Code,
        Function
    }
    
    public class Interpreter
    {
        private readonly TextWriter _writer;
        private Dictionary<string, Action<IList<string>>> _commands;
        private Dictionary<string, Action<IList<string>>> _interpreterCommands;
        private readonly Dictionary<string, int> _variables = new Dictionary<string, int>();
        private readonly Dictionary<string, IList<string>> _functions = new Dictionary<string, IList<string>>();
        private string[] _code;
        private Mode _currentMode = Mode.Normal;
        private string _currentFunction;
        private readonly string _indent = new string(' ', 4);

        public Interpreter(TextWriter writer)
        {
            _writer = writer;
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Action<IList<string>>>
            {
                ["set"] = Set,
                ["sub"] = Sub,
                ["print"] = Print,
                ["rem"] = Rem,
                ["def"] = Def,
                ["call"] = Call
            };

            _interpreterCommands = new Dictionary<string, Action<IList<string>>>
            {
                ["set"] = SetMode,
                ["end"] = EndMode,
                ["run"] = Run
            };
        }

        public void ExecuteLine(string command)
        {
            try
            {
                ExecuteInterpreterCommand(command);
            }
            catch (ArgumentException e)
            {
                _writer.WriteLine(e.Message);
            }
        }

        private void ExecuteInterpreterCommand(string command)
        {
            switch (_currentMode)
            {
                case Mode.Normal:
                    ExecuteCommand(command, _interpreterCommands);
                    break;
                case Mode.Code:
                    _code = command.Split();
                    break;
            }
        }

        private void SetMode(IList<string> args)
        {
            var newMode = ReadMode(args[0]);
            _currentMode = newMode;
        }

        private void EndMode(IList<string> args)
        {
            var mode = ReadMode(args[1]);
            if (_currentMode == mode)
                _currentMode = Mode.Normal;
            else throw new ArgumentException("Попытка завершить режим, который не запущен");
        }

        private void Run(IList<string> args)
        {
            if (_code is null)
                throw new ArgumentException("Не задан код для запуска");
            ExecuteCode(_code);
        }

        private void ExecuteCode(IList<string> source)
        {
            foreach (var line in source)
                switch (_currentMode)
                {
                    case Mode.Normal:
                        ExecuteCommand(line, _commands);
                        break;
                    case Mode.Function:
                        if (line.StartsWith(_indent))
                            _functions[_currentFunction].Add(line);
                        else
                        {
                            _currentMode = Mode.Normal;
                            _currentFunction = null;
                            goto case Mode.Normal;
                        }
                        break;
                }
        }

        private static Mode ReadMode(string name)
        {
            if (!Enum.TryParse(name, true, out Mode mode))
                throw new ArgumentException("Такого режима не существует");
            return mode;
        }

        private static void ExecuteCommand(string command, IReadOnlyDictionary<string, Action<IList<string>>> source)
        {
            var tokens = command.Split(' ');
            if (tokens.Length < 1)
                throw new ArgumentException("Нужно указать название команды");
            var commandName = tokens[0];
            var args = tokens.Skip(1).ToArray();

            var action = GetCommandAction(commandName, source);
            action.Invoke(args);
        }

        private void Set(IList<string> args)
        {
            var variable = ReadName(args[0]);
            var value = ReadValue(args[1]);
            _variables[variable] = value;
        }

        private void Sub(IList<string> args)
        {
            var variable = ReadName(args[0]);
            CheckIfSet(variable);
            var value = ReadValue(args[1]);
            _variables[variable] -= value;
        }

        private void Print(IList<string> args)
        {
            var variable = ReadName(args[0]);
            CheckIfSet(variable);
            _writer.WriteLine(_variables[variable]);
        }

        private void Rem(IList<string> args)
        {
            var variable = ReadName(args[0]);
            CheckIfSet(variable);
            _variables.Remove(variable);
        }

        private void Def(IList<string> args)
        {
            var name = ReadName(args[0]);
            _currentFunction = name;
            if (_functions.ContainsKey(name))
                throw new ArgumentException($"Функция {name} уже определена");
            _functions.Add(name, new List<string>());
        }

        private void Call(IList<string> args)
        {
            var name = ReadName(args[0]);
            if (!_functions.TryGetValue(name, out var function))
                throw new ArgumentException($"Функция с именем {name} не определена");
            ExecuteCode(function);
        }

        private static Action<IList<string>> GetCommandAction(string commandName,
            IReadOnlyDictionary<string, Action<IList<string>>> source)
        {
            if (!source.TryGetValue(commandName, out var result))
                throw new ArgumentException("Команда не содержится в списке допустимых команд");
            return result;
        }

        private static string ReadName(string arg)
        {
            if (!Regex.IsMatch(arg, @"^[a-zA-Z]+$"))
                throw new ArgumentException("Название должно состоять из букв английского алфавита");
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