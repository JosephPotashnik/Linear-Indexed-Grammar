using System;
using System.Security.Cryptography;
using System.Threading;

namespace LinearIndexedGrammarParser
{
    public static class Pseudorandom
    {
        private readonly static ThreadLocal<Random> prng = new ThreadLocal<Random>(() =>
            new Random(NextSeed()));

        private static int NextSeed()
        {
            var bytes = new byte[sizeof(int)];
            RandomNumberGenerator.Fill(bytes);
            return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        }

        public static int NextInt(int range) => prng.Value.Next(range);
        public static int NextInt() => prng.Value.Next();
        public static double NextDouble() => prng.Value.NextDouble();
    }
}
