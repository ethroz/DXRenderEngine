using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using static DXRenderEngine.Time;

namespace DXRenderEngine;

public static class Logger
{
    static Logger()
    {
        logThread = new(LogLoop);
        logThread.IsBackground = true;
        logThread.Name = "Logger";
        logThread.Start();
    }

    private static readonly Thread logThread;
    private static readonly Queue<string> messages = new();
    private static readonly Queue<int> lengths = new();
    private static readonly Queue<long> times = new();

    public static void LogLoop()
    {
        while (true)
        {
            int count = messages.Count;
            if (count == 0)
            {
                Thread.Sleep(1);
            }
            else if (count <= 10)
            {
                for (int i = 0; i < count; ++i)
                {
                    Trace.WriteLine(times.Dequeue() + ": " + messages.Dequeue());
                    lengths.Dequeue();
                }
            }
            else
            {
                int charCount = count * 22;
                for (int i = 0; i < count; ++i)
                {
                    charCount += lengths.Dequeue();
                }
                StringBuilder sb = new();
                for (int i = 0; i < count; ++i)
                {
                    sb.Append(times.Dequeue());
                    sb.Append(": ");
                    sb.Append(messages.Dequeue());
                    sb.Append('\n');
                }
                Trace.Write(sb.ToString());
            }
        }
    }

    public static void SetLogCapacity(int capacity)
    {
        messages.EnsureCapacity(capacity);
        lengths.EnsureCapacity(capacity);
        times.EnsureCapacity(capacity);
    }

    public static void print(string message)
    {
        times.Enqueue(Ticks);
        lengths.Enqueue(message.Length);
        messages.Enqueue(message);
    }

    public static void print(object message)
    {
        times.Enqueue(Ticks);
        string messageStr = message.ToString();
        lengths.Enqueue(messageStr.Length);
        messages.Enqueue(messageStr);
    }

    public static void print()
    {
        times.Enqueue(Ticks);
        lengths.Enqueue(0);
        messages.Enqueue("");
    }
}
