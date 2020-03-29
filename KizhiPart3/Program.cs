using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Text;

namespace KizhiPart3
{
    public class Program
    {
        public static void Main()
        {
            var output = new StringBuilder();
            var interpreter = new Debugger(new StringWriter(output));
            interpreter.ExecuteLine("set code");
            interpreter.ExecuteLine(@"set a 9
set b 5
def testtwo
    sub b 2
    sub b 2
call test
def test
    sub a 3
    print a
    call testtwo");
            interpreter.ExecuteLine("end set code");
            while (true)
            {
                var inputs = new List<string>();
                Label:
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input))
                {
                    interpreter.ExecuteLine(string.Join("\r\n", inputs));
                    Console.Write(output);
                    output.Clear();
                }
                else
                {
                    inputs.Add(input);
                    goto Label;
                }
            }
        }
    }
}