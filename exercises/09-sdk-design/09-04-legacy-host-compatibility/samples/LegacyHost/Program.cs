using System;
using System.Collections.Generic;
using System.Linq;
using MigrationKit.Transfer;

namespace LegacyHost
{
    /// <summary>What the customer's .NET Framework 4.8 application does with the SDK.</summary>
    internal static class Program
    {
        private static void Main()
        {
            var entries = new List<FileEntry>
            {
                new FileEntry("documents/specification.pdf", 48_000, "0".PadLeft(64, '0')),
                new FileEntry("images/deck-inspection-001.jpg", 182_000, "1".PadLeft(64, '1')),
            };

            var batches = UploadPlanner.PlanBatches(entries, batchSize: 1);
            Console.WriteLine("Batches: " + batches.Count);
            Console.WriteLine("Total bytes: " + UploadPlanner.TotalBytes(entries));
            Console.WriteLine("Images: " + entries.Count(e => e.IsImage));
            Console.WriteLine("Manifest path: " + ManifestReader.ToManifestPath(@"C:\Projects\Northwind", @"C:\Projects\Northwind\documents\specification.pdf"));
        }
    }
}
