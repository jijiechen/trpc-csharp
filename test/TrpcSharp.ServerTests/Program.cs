using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TrpcSharp.ServerTests
{
    class Program
    {
        private static int ab = 1;
        
        
        static async Task Main(string[] args)
        {


            var x1 = ReadAllAsync().GetAsyncEnumerator();
            await x1.MoveNextAsync();
            Console.WriteLine($"x1 current: {x1.Current}");
            Console.WriteLine($"x1 hashcode: {x1.GetHashCode()}");
            
            var x2 = ReadAllAsync().GetAsyncEnumerator();
            await x2.MoveNextAsync();
            Console.WriteLine($"x2 current: {x2.Current}");
            Console.WriteLine($"x2 hashcode: {x2.GetHashCode()}");
        }
        
        
        public static async IAsyncEnumerable<int> ReadAllAsync()
        {
            int prev = -1;
            await foreach(var s in ReadAllNumbers())
            {
                if (prev != -1)
                {
                    Console.WriteLine($"Code in the loop {prev}");
                }

                prev = s;
                yield return s;
                
                Console.WriteLine($"Code after one item {prev}");
            };
            
            Console.WriteLine($"Code after all items {prev}");
        }
        
        
        public static async IAsyncEnumerable<int> ReadAllNumbers()
        {
            do
            {
                yield return await Task.FromResult(ab);
            } while (++ab <= 20);
        }
    }
}