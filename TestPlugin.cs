using AzureAIAgent.Plugins;
using System;

namespace TestAccess
{
    public class TestClass
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Testing AzureResourcePlugin access...");
            var plugin = new AzureResourcePlugin();
            Console.WriteLine($"Plugin created successfully: {plugin.GetType().Name}");
        }
    }
}
