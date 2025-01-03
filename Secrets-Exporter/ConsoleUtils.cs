using System.Text;

namespace Secrets_Exporter;

public static class ConsoleUtils
{
    public static string ReadPassword()
    {
        var password = new StringBuilder();
        
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
        }

        return password.ToString();
    }
    
    public static void PauseProcess(string message)
    {
        Console.WriteLine(message);
        Console.ReadLine();
        Environment.Exit(0);
    }
}