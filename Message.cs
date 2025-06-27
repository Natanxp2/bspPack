namespace bspPack;

class Message
{
    public static void Success(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);
        Console.ResetColor();
    }
    public static void Info(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    public static void Warning(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    public static void Error(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    public static void Write(string msg, ConsoleColor? color = null)
    {
        if (color.HasValue)
        {
            Console.ForegroundColor = color.Value;
            Console.Write(msg);
            Console.ResetColor();
        }
        else
            Console.Write(msg);
    }

    public static void WriteLine(string msg, ConsoleColor? color = null)
    {
        if (color.HasValue)
        {
            Console.ForegroundColor = color.Value;
            Console.WriteLine(msg);
            Console.ResetColor();
        }
        else
            Console.WriteLine(msg);
    }

    public static string Prompt(string prompt, ConsoleColor? color = null)
    {
        Write(prompt, color);
        return Console.ReadLine() ?? string.Empty;
    }
}