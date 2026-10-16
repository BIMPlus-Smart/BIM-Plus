using System;

class Program
{
    static void Main()
    {
        double span = 1884.0;
        double spacing = 150.0;
        
        int numGaps = (int)Math.Ceiling(span / spacing);
        double actualSpacing = span / numGaps;
        int numBars = numGaps + 1;
        
        Console.WriteLine(String.Format("span: {0}, spacing: {1}", span, spacing));
        Console.WriteLine(String.Format("numGaps: {0}, actualSpacing: {1}, numBars: {2}", numGaps, actualSpacing, numBars));
        
        for (int i = 0; i < numBars; i++)
        {
            double current = i * actualSpacing;
            Console.WriteLine(String.Format("Bar {0}: {1}", i, current));
        }
    }
}
