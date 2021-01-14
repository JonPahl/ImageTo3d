using System;

namespace ImageTo3d
{

    class Program
    {
        static void Help()
        {
            Options defaults = new Options();

            string[] help = new string[]
            {
                "ImageTo3D: converts an image file to a 3d STL file.",
                "           by default thickness of the model depends on darkness of the image thicker being darker",
                "",
                "    ImageTo3D [-b] [-t] [-n] [-mx] [-my] [-w <width-in-mm>] [-minthick <thick-in-mm>]",
                "              [-noborder] [-borderthick <value-in-mm>] [-borderwidth <value-in-mm>]",
                "              [-maxthick <thick-in-mm>] <image-file> [<output-stl-file>]",
                "",
                "       -b              set output format to binary (default)",
                "       -t              set output format to text",
                "       -n              use the negative image",
                "       -mx             mirror image in X",
                "       -my             mirror image in Y",
                "       -w              set desired width (default " + defaults.DesiredWidth + ")",
                "       -noborder       do not add border round image",
                "       -borderthick    thickness of border in millimeters (default " + defaults.BorderThickness + ")",
                "       -borderwidth    width of border in millimeters (default " + defaults.BorderWidth + ")",
                "       -minthick       set minimum thickness in millimeters (default " + defaults.MinThickness + ")",
                "       -maxthick       set mmaximum thickness in millimeters (default " + defaults.MaxThickness + ")"
            };

            foreach (string s in help)
                Console.WriteLine(s);
        }

        static void Main(string[] args)
        {
            Options options = new Options();

            string inFile = null;
            string outFile = null;

            try
            {
                for (int i = 0; i < args.Length; ++i)
                {
                    string arg = args[i];
                    if (arg[0] == '-')
                    {
                        if (arg == "-b")
                            options.Binary = true;
                        else if (arg == "-t")
                            options.Binary = false;
                        else if (arg == "-n")
                            options.Negative = true;
                        else if (arg == "-mx")
                            options.MirrorX = true;
                        else if (arg == "-my")
                            options.MirrorY = true;
                        else if (arg == "-noborder")
                            options.AddBorder = false;
                        else if (arg == "-w")
                        {
                            if (i >= args.Length - 1) throw new ArgumentException(String.Format("Expected value after {0}", arg));
                            options.DesiredWidth = Single.Parse(args[++i]);
                        }
                        else if (arg == "-minthick")
                        {
                            if (i >= args.Length - 1) throw new ArgumentException(String.Format("Expected value after {0}", arg));
                            options.MinThickness = Single.Parse(args[++i]);
                        }
                        else if (arg == "-maxthick")
                        {
                            if (i >= args.Length - 1) throw new ArgumentException(String.Format("Expected value after {0}", arg));
                            options.MaxThickness = Single.Parse(args[++i]);
                        }
                        else if (arg == "-borderthick")
                        {
                            if (i >= args.Length - 1) throw new ArgumentException(String.Format("Expected value after {0}", arg));
                            options.BorderThickness = Single.Parse(args[++i]);
                        }
                        else if (arg == "-borderwidth")
                        {
                            if (i >= args.Length - 1) throw new ArgumentException(String.Format("Expected value after {0}", arg));
                            options.BorderWidth = Single.Parse(args[++i]);
                        }
                        else if (arg == "-help")
                        {
                            Help();
                            System.Environment.Exit(0);
                        }
                        else
                        {
                            throw new ArgumentException("Unrecognized switch: " + arg);
                        }
                    }
                    else if (inFile == null)
                        inFile = arg;
                    else if (outFile == null)
                        outFile = arg;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Bad command line: {0}, use -help for description", ex.Message);
                Environment.Exit(1);
            }

            if (inFile == null)
            {
                Console.WriteLine("No input file given");
                System.Environment.Exit(1);
            }

            Generator g = new Generator(options);
            g.ProcessFile(inFile, outFile);
        }
    }
}
