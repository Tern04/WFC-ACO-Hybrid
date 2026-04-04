using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace _Project.Scripts.Utils
{
    /// <summary>
    /// Utility class for automated algorithmic performance profiling.
    /// Gathers statistical metrics and exports data to a CSV format.
    /// </summary>
    public static class EntropyBenchmark
    {
        /// <summary>
        /// Executes a benchmarking suite across defined environmental configurations.
        /// </summary>
        /// <param name="fileName">Target CSV file name</param>
        /// <param name="iterations">Number of iterations per configuration</param>
        /// <param name="testSizes">Array of grid dimensions to evaluate</param>
        /// <param name="testAction">Delegate returning a tuple of (TotalTimeMs, EntropySearchTimeMs)</param>
        public static void RunBenchmark(string fileName, int iterations, Vector3Int[] testSizes, Func<Vector3Int, (double TotalTime, double EntropyTime)> testAction)
        {
            StringBuilder csv = new StringBuilder();
            
            // Expanded header
            csv.AppendLine("MapSize;CellCount;AvgTotal_ms;AvgEntropy_ms;MedianTotal_ms;MedianEntropy_ms;StdDevTotal_ms;StdDevEntropy_ms;MinTotal_ms;MinEntropy_ms;MaxTotal_ms;MaxEntropy_ms");
            
            foreach (Vector3Int size in testSizes)
            {
                List<double> totalTimes = new List<double>();
                List<double> entropyTimes = new List<double>();

                for (int i = 0; i < iterations; i++)
                {
                    // Invoke the provided test action and collect results
                    var result = testAction.Invoke(size);
                    totalTimes.Add(result.TotalTime);
                    entropyTimes.Add(result.EntropyTime);
                }
                
                int cellCount = size.x * size.y * size.z;

                // Compute statistical metrics for Total Time
                double avgTotal = totalTimes.Average();
                double medTotal = GetMedian(totalTimes);
                double stdDevTotal = GetStdDev(totalTimes, avgTotal);
                double minTotal = totalTimes.Min();
                double maxTotal = totalTimes.Max();

                // Compute statistical metrics for Entropy Search Time
                double avgEntropy = entropyTimes.Average();
                double medEntropy = GetMedian(entropyTimes);
                double stdDevEntropy = GetStdDev(entropyTimes, avgEntropy);
                double minEntropy = entropyTimes.Min();
                double maxEntropy = entropyTimes.Max();
                
                // Append all metrics to the CSV row
                csv.AppendLine($"{size.x}x{size.y}x{size.z};{cellCount};{avgTotal:F2};{avgEntropy:F2};{medTotal:F2};{medEntropy:F2};{stdDevTotal:F2};{stdDevEntropy:F2};{minTotal:F2};{minEntropy:F2};{maxTotal:F2};{maxEntropy:F2}");
            }

            string filePath = Path.Combine(Application.dataPath, fileName);
            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Returns the median value of a list of doubles.
        /// </summary>
        /// <param name="list">List of double values to calculate the median from</param>
        /// <returns></returns>
        private static double GetMedian(List<double> list)
        {
            var sorted = list.OrderBy(n => n).ToList();
            int mid = sorted.Count / 2;
            return (sorted.Count % 2 != 0) ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        /// <summary>
        /// Returns the standard deviation of a list of doubles.
        /// </summary>
        /// <param name="list">List of double values to calculate the standard deviation from</param>
        /// <param name="avg">Pre-calculated average of the list</param>
        /// <returns></returns>
        private static double GetStdDev(List<double> list, double avg)
        {
            double sum = list.Sum(d => Math.Pow(d - avg, 2));
            return Math.Sqrt(sum / list.Count);
        }
        
    }
}