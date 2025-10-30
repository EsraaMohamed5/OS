using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OS_Project
{
    internal class SuperblockManager
    {
        private readonly virtualDisk _disk;

        public SuperblockManager(virtualDisk disk)
        {
            _disk = disk;

            byte[] superblock = _disk.ReadCluster(FSConstants.SUPERBLOCK_CLUSTER);
            bool allZero = true;
            foreach (byte b in superblock)
            {
                if (b != 0)
                {
                    allZero = false;
                    break;
                }
            }

            if (!allZero)
                return;

            byte[] emptySuperblock = new byte[FSConstants.CLUSTER_SIZE];
            _disk.WriteCluster(FSConstants.SUPERBLOCK_CLUSTER, emptySuperblock);
        }

        public byte[] ReadSuperblock()
        {
            return _disk.ReadCluster(FSConstants.SUPERBLOCK_CLUSTER);
        }

        public void WriteSuperblock(byte[] data)
        {
            if (data.Length != FSConstants.CLUSTER_SIZE)
                throw new ArgumentException("Superblock size must be exactly 1024 bytes.");

            _disk.WriteCluster(FSConstants.SUPERBLOCK_CLUSTER, data);
        }
    }
}
