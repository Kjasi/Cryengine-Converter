﻿using System;
using System.Threading;
using System.Globalization;
using CgfConverter;

namespace CgfConverterConsole
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Utils.LogLevel = LogLevelEnum.Warning;
            Utils.DebugLevel = LogLevelEnum.Debug;

            string oldTitle = Console.Title;

#if DEV_MARKEMP
            Utils.LogLevel = LogLevelEnum.Verbose; // Display ALL error logs in the console
            Utils.DebugLevel = LogLevelEnum.Debug;  // Send all to the IDE Output window
#endif


            CultureInfo customCulture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            Thread.CurrentThread.CurrentCulture = customCulture;
            ArgsHandler argsHandler = new ArgsHandler();
#if !DEBUG
            try
            {
#endif
                if (argsHandler.ProcessArgs(args) == 0)
                {
                    foreach (string inputFile in argsHandler.InputFiles)
                    {
                        try
                        {
                            // Read CryEngine Files
                            CryEngine cryData = new CryEngine(inputFile, argsHandler.DataDir.FullName);
                            cryData.ProcessCryengineFiles();

                            if (argsHandler.OutputBlender == true)
                            {
                                Blender blendFile = new Blender(argsHandler, cryData);

                                blendFile.Render(argsHandler.OutputDir, argsHandler.InputFiles.Count > 1);
                            }

                            if (argsHandler.OutputWavefront == true)
                            {
                                Wavefront objFile = new Wavefront(argsHandler, cryData);

                                objFile.Render(argsHandler.OutputDir, argsHandler.InputFiles.Count > 1);
                            }

                            if (argsHandler.OutputCryTek == true)
                            {
                                CryRender cryFile = new CryRender(argsHandler, cryData);

                                cryFile.Render(argsHandler.OutputDir, argsHandler.InputFiles.Count > 1);
                            }

                            if (argsHandler.OutputCollada == true)
                            {
                                COLLADA daeFile = new COLLADA(argsHandler, cryData);

                                daeFile.Render(argsHandler.OutputDir, argsHandler.InputFiles.Count > 1);
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.Log(LogLevelEnum.Critical);
                            Utils.Log(LogLevelEnum.Critical, "********************************************************************************");
                            Utils.Log(LogLevelEnum.Critical, "There was an error rendering {0}", inputFile);
                            Utils.Log(LogLevelEnum.Critical);
                            Utils.Log(LogLevelEnum.Critical, ex.Message);
                            Utils.Log(LogLevelEnum.Critical);
                            Utils.Log(LogLevelEnum.Critical, ex.StackTrace);
                            Utils.Log(LogLevelEnum.Critical, "********************************************************************************");
                            Utils.Log(LogLevelEnum.Critical);
                            return 1;
                        }
                    }
                }

#if !DEBUG
            }
            catch (Exception)
            {
                if (argsHandler.Throw)
                    throw;
            }
#endif

            Console.Title = oldTitle;

            Utils.Log(LogLevelEnum.Debug, "Done...");
            
            return 0;
        }
    }
}