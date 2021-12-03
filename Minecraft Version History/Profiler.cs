using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public static class Profiler
    {
        private static readonly Stack<(Stopwatch watch, string name)> TimerStack = new();
        public static void Start(string name)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{new String(' ', TimerStack.Count * 2)}@ {name}");
            Console.ResetColor();
            var timer = new Stopwatch();
            timer.Start();
            TimerStack.Push((timer, name));
        }

        public static void Stop()
        {
            var (timer, name) = TimerStack.Pop();
            timer.Stop();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{new String(' ', TimerStack.Count * 2)}@ {name}: {timer.Elapsed}");
            Console.ResetColor();
        }

        public static void Run(string name, Action action)
        {
            Start(name);
            action();
            Stop();
        }
    }
}
