﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AutoTOC
{
    class Program
    {
        enum MEGame
        {
            ME3,
            LE1,
            LE2,
            LE3
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Detecting game...");
            string gameDir;

            if (args.Length == 1)
            {
                // Path is passed in, hopefully is game root directory or .exe
                gameDir = args[0];
                if (gameDir.EndsWith("\""))
                {
                    gameDir = gameDir.Remove(gameDir.Length - 1);
                }
                else if (gameDir.EndsWith(".exe"))
                {
                    gameDir = GetGamepathFromExe(gameDir);
                }
            }
            else if (args.Length == 2 && args[0] == "-r")
            {
                try {
                    MEGame game = (MEGame)Enum.Parse(typeof(MEGame), args[1], true);

                    gameDir = GetGamepathFromRegistry(game);
                    if(game != MEGame.ME3)
                    {
                        switch(game){
                            case MEGame.LE1:
                                gameDir = Path.Combine(gameDir, "ME1");
                                break;
                            case MEGame.LE2:
                                gameDir = Path.Combine(gameDir, "ME2");
                                break;
                            case MEGame.LE3:
                                gameDir = Path.Combine(gameDir, "ME3");
                                break;
                            default:
                                throw new ArgumentException();
                        }
                    }
                }
                catch (ArgumentException e){
                    Console.WriteLine("Not a supported Mass Effect game");
                    return;
                }
                catch {
                    Console.WriteLine("Unable to detect gamepath from registry");
                    return;
                }
            }
            else
            {
                Console.WriteLine("Requires one argument: the install dir or .exe of the game you're trying to TOC.");
                Console.WriteLine("(eg. \"D:\\Origin Games\\Mass Effect Legendary Edition\\ME3\")");
                Console.WriteLine("Detect game from registry with -r {game}. Options: ME3, LE1, LE2, LE3");
                return;
            }

            Console.WriteLine("Generating TOCs...");
            GenerateTocFromGamedir(gameDir);
            Console.WriteLine("Done!");
        }

        static void GenerateTocFromGamedir(string gameDir)
        {
            string baseDir = Path.Combine(gameDir, @"BIOGame\");
            string dlcDir = Path.Combine(baseDir, @"DLC\");
            List<string> folders = (new DirectoryInfo(dlcDir)).GetDirectories().Select(d => d.FullName).ToList();
            folders.Add(baseDir);
            Task.WhenAll(folders.Select(loc => TOCAsync(loc))).Wait();
        }

        static Task TOCAsync(string tocLoc)
        {
            return Task.Run(() => PrepareToCreateTOC(tocLoc));
        }

        static void PrepareToCreateTOC(string consoletocFile)
        {
            if (!consoletocFile.EndsWith("\\")) consoletocFile += "\\";
            List<string> files = GetFiles(consoletocFile);

            if (files.Count > 0)
            {
                string t = files[0];
                int n = t.LastIndexOf("DLC_");
                if (n > 0)
                {
                    for (int i = 0; i < files.Count; i++)
                        files[i] = files[i].Substring(n);
                    string t2 = files[0];
                    n = t2.IndexOf("\\");
                    for (int i = 0; i < files.Count; i++)
                        files[i] = files[i].Substring(n + 1);
                }
                else
                {
                    n = t.LastIndexOf("BIOGame");
                    if (n > 0)
                    {
                        for (int i = 0; i < files.Count; i++)
                            files[i] = files[i].Substring(n);
                    }
                }
                string pathbase;
                string t3 = files[0];
                int n2 = t3.LastIndexOf("BIOGame");
                if (n2 >= 0)
                {
                    pathbase = Path.GetDirectoryName(Path.GetDirectoryName(consoletocFile)) + "\\";
                }
                else
                {
                    pathbase = consoletocFile;
                }
                CreateTOC(pathbase, consoletocFile + "PCConsoleTOC.bin", files.ToArray());
            }
        }

        static void CreateTOC(string basepath, string tocFile, string[] files)
        {
            FileStream fs = new FileStream(tocFile, FileMode.Create, FileAccess.Write);
            fs.Write(BitConverter.GetBytes((int)0x3AB70C13), 0, 4);
            fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
            fs.Write(BitConverter.GetBytes((int)0x1), 0, 4);
            fs.Write(BitConverter.GetBytes((int)0x8), 0, 4);
            fs.Write(BitConverter.GetBytes((int)files.Length), 0, 4);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (i == files.Length - 1)//Entry Size
                    fs.Write(new byte[2], 0, 2);
                else
                    fs.Write(BitConverter.GetBytes((ushort)(0x1D + file.Length)), 0, 2);
                fs.Write(BitConverter.GetBytes((ushort)0), 0, 2);//Flags
                if (Path.GetFileName(file).ToLower() != "pcconsoletoc.bin")
                {
                    FileStream fs2 = new FileStream(basepath + file, FileMode.Open, FileAccess.Read);
                    fs.Write(BitConverter.GetBytes((int)fs2.Length), 0, 4);//Filesize
                    fs2.Close();
                }
                else
                {
                    fs.Write(BitConverter.GetBytes((int)0), 0, 4);//Filesize
                }
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);//SHA1
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                foreach (char c in file)
                    fs.WriteByte((byte)c);
                fs.WriteByte(0);
            }
            fs.Close();
        }

        static List<string> GetFiles(string basefolder)
        {
            List<string> res = new List<string>();
            string test = Path.GetFileName(Path.GetDirectoryName(basefolder));
            string[] files = GetTocableFiles(basefolder);
            res.AddRange(files);
            DirectoryInfo folder = new DirectoryInfo(basefolder);
            DirectoryInfo[] folders = folder.GetDirectories();
            if (folders.Length != 0)
                if (test != "BIOGame")
                    foreach (DirectoryInfo f in folders)
                        res.AddRange(GetFiles(basefolder + f.Name + "\\"));
                else
                    foreach (DirectoryInfo f in folders)
                        if (f.Name == "CookedPCConsole" || f.Name == "Movies" || f.Name == "Splash")
                            res.AddRange(GetFiles(Path.Combine(basefolder, f.Name)));
                        else if (f.Name == "Content")
                            res.AddRange(GetFiles(Path.Combine(basefolder, f.Name, "Packages\\ISACT")));
            
            return res;
        }

        static string[] Pattern = { "*.pcc", "*.afc", "*.bik", "*.bin", "*.tlk", "*.txt", "*.cnd", "*.upk", "*.tfc", "*.isb" };

        static string[] GetTocableFiles(string path)
        {
            List<string> res = new List<string>();
            foreach (string s in Pattern)
                res.AddRange(Directory.GetFiles(path, s));
            return res.ToArray();
        }

        static string[] ValidExecutables = { "MassEffect1.exe", "MassEffect2.exe", "MassEffect3.exe" };

        static string GetGamepathFromExe(string path)
        {
            if(path != null && ValidExecutables.Any((exe) => path.EndsWith(exe)))
            {
                return path.Substring(0, path.LastIndexOf("Binaries", StringComparison.OrdinalIgnoreCase));
            }
            else throw new ArgumentException("Executable file is not a supported Mass Effect game.");
        }

        static string GetGamepathFromRegistry(MEGame game)
        {
            if(game != MEGame.ME3)
            {
                // TODO
                // Get LE path from registry
                throw new NotImplementedException("LE registry key unknown");
            }
            else
            {
                // Get ME3 path from registry
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                //string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                string subkey = @"BioWare\Mass Effect 3";

                string keyName = hkey32 + subkey;
                return (string)Registry.GetValue(keyName, "Install Dir", null);
            }

        }
    }
}
