//
// Copyright (c) 2008-2010, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using DiscUtils;
using DiscUtils.Common;
using DiscUtils.LogicalDiskManager;
using DiscUtils.Partitions;

namespace DiskDump
{
    class Program : ProgramBase
    {
        private CommandLineMultiParameter _inFiles;
        private CommandLineSwitch _showContent;
        private CommandLineSwitch _showVolContent;
        private CommandLineSwitch _showFiles;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _inFiles = FileOrUriMultiParameter("disk", "Paths to the disks to inspect.  Where a volume manager is used to span volumes across multiple virtual disks, specify all disks in the set.", false);
            _showContent = new CommandLineSwitch("db", "diskbytes", null, "Includes a hexdump of all disk content in the output");
            _showVolContent = new CommandLineSwitch("vb", "volbytes", null, "Includes a hexdump of all volumes content in the output");
            _showFiles = new CommandLineSwitch("sf", "showfiles", null, "Includes a list of all files found in volumes");

            parser.AddMultiParameter(_inFiles);
            parser.AddSwitch(_showContent);
            parser.AddSwitch(_showVolContent);
            parser.AddSwitch(_showFiles);

            return StandardSwitches.UserAndPassword;
        }

        protected override void DoRun()
        {
            List<VirtualDisk> disks = new List<VirtualDisk>();
            foreach (var path in _inFiles.Values)
            {
                VirtualDisk disk = VirtualDisk.OpenDisk(path, FileAccess.Read, UserName, Password);
                disks.Add(disk);

                Console.WriteLine();
                Console.WriteLine("DISK: " + path);
                Console.WriteLine();
                Console.WriteLine("       Capacity: {0:X16}", disk.Capacity);
                Console.WriteLine("       Geometry: {0}", disk.Geometry);
                Console.WriteLine("  BIOS Geometry: {0}", disk.BiosGeometry);
                Console.WriteLine("      Signature: {0:X8}", disk.Signature);
                if (disk.IsPartitioned)
                {
                    Console.WriteLine("           GUID: {0}", disk.Partitions.DiskGuid);
                }
                Console.WriteLine();


                Console.WriteLine();
                Console.WriteLine("  Stored Extents");
                Console.WriteLine();
                foreach (var extent in disk.Content.Extents)
                {
                    Console.WriteLine("    {0:X16} - {1:X16}", extent.Start, extent.Start + extent.Length);
                }
                Console.WriteLine();


                Console.WriteLine();
                Console.WriteLine("  Partitions");
                Console.WriteLine();
                if (disk.IsPartitioned)
                {
                    Console.WriteLine("    T   Start (bytes)     End (bytes)       Type");
                    Console.WriteLine("    ==  ================  ================  ==================");
                    foreach (var partition in disk.Partitions.Partitions)
                    {
                        Console.WriteLine("    {0:X2}  {1:X16}  {2:X16}  {3}", partition.BiosType, partition.FirstSector * 512, partition.LastSector * 512 + 512, partition.TypeAsString);

                        BiosPartitionInfo bpi = partition as BiosPartitionInfo;
                        if (bpi != null)
                        {
                            Console.WriteLine("        {0,-16}  {1}", bpi.Start.ToString(), bpi.End.ToString());
                            Console.WriteLine();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("    No partitions");
                    Console.WriteLine();
                }
            }


            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("VOLUMES");
            Console.WriteLine();
            VolumeManager volMgr = new VolumeManager();
            foreach (var disk in disks)
            {
                volMgr.AddDisk(disk);
            }

            try
            {
                Console.WriteLine();
                Console.WriteLine("  Physical Volumes");
                Console.WriteLine();
                foreach (var vol in volMgr.GetPhysicalVolumes())
                {
                    Console.WriteLine("  " + vol.Identity);
                    Console.WriteLine("    Type: " + vol.VolumeType);
                    Console.WriteLine("    BIOS Type: " + vol.BiosType.ToString("X2") + " [" + BiosPartitionTypes.ToString(vol.BiosType) + "]");
                    Console.WriteLine("    Size: " + vol.Length);
                    Console.WriteLine("    Disk Id: " + vol.DiskIdentity);
                    Console.WriteLine("    Disk Sig: " + vol.DiskSignature.ToString("X8"));
                    Console.WriteLine("    Partition: " + vol.PartitionIdentity);
                    Console.WriteLine("    Disk Geometry: " + vol.PhysicalGeometry);
                    Console.WriteLine("    BIOS Geometry: " + vol.BiosGeometry);
                    Console.WriteLine("    First Sector: " + vol.PhysicalStartSector);
                    Console.WriteLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            try
            {
                Console.WriteLine();
                Console.WriteLine("  Logical Volumes");
                Console.WriteLine();
                foreach (var vol in volMgr.GetLogicalVolumes())
                {
                    Console.WriteLine("  " + vol.Identity);
                    Console.WriteLine("    BIOS Type: " + vol.BiosType.ToString("X2") + " [" + BiosPartitionTypes.ToString(vol.BiosType) + "]");
                    Console.WriteLine("    Status: " + vol.Status);
                    Console.WriteLine("    Size: " + vol.Length);
                    Console.WriteLine("    Disk Geometry: " + vol.PhysicalGeometry);
                    Console.WriteLine("    BIOS Geometry: " + vol.BiosGeometry);
                    Console.WriteLine("    First Sector: " + vol.PhysicalStartSector);

                    DiscUtils.FileSystemInfo[] fileSystemInfos = FileSystemManager.DetectDefaultFileSystems(vol);
                    Console.WriteLine("    File Systems: " + string.Join<DiscUtils.FileSystemInfo>(", ", fileSystemInfos));

                    Console.WriteLine();

                    if (_showVolContent.IsPresent)
                    {
                        Console.WriteLine("    Binary Contents...");
                        try
                        {
                            using (Stream s = vol.Open())
                            {
                                HexDump.Generate(s, Console.Out);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                        Console.WriteLine();
                    }

                    if (_showFiles.IsPresent)
                    {
                        foreach (var fsi in fileSystemInfos)
                        {
                            Console.WriteLine("    Files ({0})...", fsi.Name);
                            using (DiscFileSystem fs = fsi.Open(vol))
                            {
                                ShowDir(fs.Root, 6);
                            }
                            Console.WriteLine();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            try
            {
                bool foundDynDisk = false;
                DynamicDiskManager dynDiskManager = new DynamicDiskManager();
                foreach (var disk in disks)
                {
                    if (DynamicDiskManager.IsDynamicDisk(disk))
                    {
                        dynDiskManager.Add(disk);
                        foundDynDisk = true;
                    }
                }
                if (foundDynDisk)
                {
                    Console.WriteLine();
                    Console.WriteLine("  Logical Disk Manager Info");
                    Console.WriteLine();
                    dynDiskManager.Dump(Console.Out, "  ");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            if (_showContent.IsPresent)
            {
                foreach (var path in _inFiles.Values)
                {
                    VirtualDisk disk = VirtualDisk.OpenDisk(path, FileAccess.Read, UserName, Password);

                    Console.WriteLine();
                    Console.WriteLine("DISK CONTENTS ({0})", path);
                    Console.WriteLine();
                    HexDump.Generate(disk.Content, Console.Out);
                    Console.WriteLine();
                }
            }
        }


        private static void ShowDir(DiscDirectoryInfo dirInfo, int indent)
        {
            Console.WriteLine("{0}{1,-50} [{2}]", new String(' ', indent), dirInfo.FullName, dirInfo.CreationTimeUtc);
            foreach (DiscDirectoryInfo subDir in dirInfo.GetDirectories())
            {
                ShowDir(subDir, indent + 0);
            }
            foreach (DiscFileInfo file in dirInfo.GetFiles())
            {
                Console.WriteLine("{0}{1,-50} [{2}]", new String(' ', indent), file.FullName, file.CreationTimeUtc);
            }
        }
    }
}
