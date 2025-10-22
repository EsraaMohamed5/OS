using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OS_Project
{
    internal class virtualDisk
    {
        private string diskPath;
        private FileStream diskStream;
        private const int ClusterSize = 1024; 
        private const int TotalClusters = 1024; 

        public void Initialize(string path, bool createIfMissing = true)
        {
            diskPath = path;

            if (!File.Exists(path))
            {
                if (!createIfMissing)
                    throw new FileNotFoundException("Disk file not found.");

                
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    byte[] empty = new byte[ClusterSize];
                    for (int i = 0; i < TotalClusters; i++)
                        fs.Write(empty, 0, ClusterSize);
                }
            }

            diskStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
        }

        public byte[] ReadCluster(int clusterNumber)
        {
            if (clusterNumber < 0 || clusterNumber >= TotalClusters)
                throw new ArgumentOutOfRangeException(nameof(clusterNumber), "Invalid cluster number.");

            byte[] buffer = new byte[ClusterSize];
            diskStream.Seek(clusterNumber * ClusterSize, SeekOrigin.Begin);
            int bytesRead = diskStream.Read(buffer, 0, ClusterSize);

            if (bytesRead < ClusterSize)
                throw new IOException("Failed to read full cluster.");

            return buffer;
        }

        public void WriteCluster(int clusterNumber, byte[] data)
        {
            if (clusterNumber < 0 || clusterNumber >= TotalClusters)
                throw new ArgumentOutOfRangeException(nameof(clusterNumber), "Invalid cluster number.");
            if (data.Length != ClusterSize)
                throw new ArgumentException($"Data must be exactly {ClusterSize} bytes.", nameof(data));

            diskStream.Seek(clusterNumber * ClusterSize, SeekOrigin.Begin);
            diskStream.Write(data, 0, ClusterSize);
            diskStream.Flush();  
        }

        public long GetDiskSize()
        {
            return ClusterSize * TotalClusters;
        }

        public void CloseDisk()
        {
            diskStream?.Flush();
            diskStream?.Close();
            diskStream = null;
        }
    }
}
