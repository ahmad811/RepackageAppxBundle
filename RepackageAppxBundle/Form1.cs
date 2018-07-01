using System;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using log4net;

/**
 * Idea was taken from https://www.codeproject.com/Tips/1193583/WebControls/
 * */
namespace RepackageAppxBundle
{
    public partial class Form1 : Form
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        static string OUT_FOLDER = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        static string APPXBUNDLE_EXTRACTED_FOLDER = Path.Combine(OUT_FOLDER, "appxbundle");
        static string APPX_EXTRACTED_FOLDER = Path.Combine(OUT_FOLDER, "appx");
        public Form1()
        {
            InitializeComponent();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            ReplacePackage();
            Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ValidateOK();
        }

        private void appxLocationText_TextChanged(object sender, EventArgs e)
        {
            ValidateOK();
        }

        private void ValidateOK()
        {
            bool valid = !string.IsNullOrEmpty(appxLocationText.Text) && Directory.Exists(appxLocationText.Text);
            okButton.Enabled = valid;
        }
        private static string WinKitPath => System.Configuration.ConfigurationManager.AppSettings["WinKitx86"];
        private void ReplacePackage()
        {
            log.InfoFormat("Running as {0}", Name);
            log.Error("This will appear in red in the console and still be written to file!");
            UpdateStatus("Starting...");
            string appxBundleFile = GetFile(appxLocationText.Text, "*.appxbundle");
            //unbundle
            string args = "unbundle /o /d " + APPXBUNDLE_EXTRACTED_FOLDER + " /p " + appxBundleFile;
            string exe = GetExecutableOfWinKit("makeappx.exe");
            RunCommand(exe, args, (result) =>
            {
                if (result)
                {
                    //unpack
                    string appxFile = GetFile(APPXBUNDLE_EXTRACTED_FOLDER, "*.appx");
                    args = "unpack /o /d " + APPX_EXTRACTED_FOLDER + " /p " + appxFile + " /l";
                    exe = GetExecutableOfWinKit("makeappx.exe");
                    RunCommand(exe, args, (result2) =>
                     {
                         if (result2)
                         {
                             //do your changes
                             RunCommand("explorer.exe", APPX_EXTRACTED_FOLDER, (result3) => { });
                             MessageBox.Show("Do your changes and close me when you are done", "Re Package");

                             //Do Pack again
                             string newAppx = Path.Combine(APPXBUNDLE_EXTRACTED_FOLDER, Path.GetFileName(appxFile));
                             args = "pack /l /o /d " + APPX_EXTRACTED_FOLDER + " /p " + newAppx;
                             exe = GetExecutableOfWinKit("makeappx.exe");
                             RunCommand(exe, args, (result4) =>
                             {
                                 if (result4)
                                 {
                                     //Make Cert
                                     exe = GetExecutableOfWinKit("MakeCert.exe");
                                     string publisher, codingSign;
                                     string cerFile = GetFile(appxLocationText.Text, "*.cer");
                                     GetCerticationInfo(cerFile, out publisher, out codingSign);
                                     string date = String.Format("{0:MM/dd/yyyy}", DateTime.Now.AddYears(5));
                                     args = "/n " + publisher + " /r /h 0 /eku " + codingSign + " /e " + date + " /sv " + Path.Combine(OUT_FOLDER, "MyKey.pvk") + " " + Path.Combine(OUT_FOLDER, "MyKey.cer");
                                     args = "/n " + publisher + " /r /h 0 /eku " + codingSign + " /e " + date + " " + Path.Combine(OUT_FOLDER, "MyKey.pvk") + " " + Path.Combine(OUT_FOLDER, "MyKey.cer");
                                     RunCommand(exe, args, (result5) =>
                                     {
                                         if (result5)
                                         {
                                             exe = GetExecutableOfWinKit("Pvk2Pfx.exe");
                                             args = "/pvk " + Path.Combine(OUT_FOLDER, "MyKey.pvk") + " /pi testpass /spc " + Path.Combine(OUT_FOLDER, "MyKey.cer") + " /pfx " + Path.Combine(OUT_FOLDER, "MyKey.pfx") + " /po testpass";
                                             RunCommand(exe, args, (result6) =>
                                             {
                                                 if (result6)
                                                 {
                                                     //Sign the package
                                                     exe = GetExecutableOfWinKit("SignTool.exe");
                                                     args = "sign /a /v /fd SHA256 /f " + Path.Combine(OUT_FOLDER, "MyKey.pfx") + " /p testpass " + newAppx;
                                                     RunCommand(exe, args, (result7) =>
                                                     {
                                                         if (result7)
                                                         {
                                                             //Make bundle
                                                             exe = GetExecutableOfWinKit("makeappx.exe");
                                                             args = "bundle /v /o /d " + APPXBUNDLE_EXTRACTED_FOLDER + " /p " + appxBundleFile;
                                                             RunCommand(exe, args, (result8) =>
                                                              {
                                                                  if (result8)
                                                                  {
                                                                      //sign bundle
                                                                      exe = GetExecutableOfWinKit("SignTool.exe");
                                                                      args = "sign /a /v /fd SHA256 /f " + Path.Combine(OUT_FOLDER, "MyKey.pfx") + " /p testpass " + appxBundleFile;
                                                                      RunCommand(exe, args, (result9) =>
                                                                      {
                                                                          if (result9)
                                                                          {
                                                                              MessageBox.Show("Re packaging the bundle done successfully!", "Repackage Bundle");
                                                                          }
                                                                          else
                                                                          {
                                                                              MessageBox.Show("Re packaging the bundle had issues!", "Repackage Bundle");
                                                                          }
                                                                      });
                                                                  }
                                                              });
                                                         }
                                                     });
                                                 }
                                             });
                                         }
                                     });
                                 }
                             });
                         }
                     });
                }
            });
        }
        private static string GetExecutableOfWinKit(string name)
        {
            //string exe = ".\\appx\\" + name;
            string exe = Path.Combine(WinKitPath, name);
            return exe;
        }
        private string FindPublisher()
        {
            string ret = "";
            //AppxBundleManifest.xml
            string file = "";
            string[] files = Directory.GetFiles(APPXBUNDLE_EXTRACTED_FOLDER, "*.xml", SearchOption.AllDirectories);
            foreach (string f in files)
            {
                if (Path.GetFileName(f) == "AppxBundleManifest.xml")
                {
                    file = f;
                    break;
                }
            }
            if (File.Exists(file))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(file);
                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("bundle", doc.DocumentElement.NamespaceURI);
                XmlNode node = doc.DocumentElement.SelectSingleNode("//bundle:Identity", nsmgr);
                if (node != null)
                {
                    XmlAttribute attr = node.Attributes["Publisher"];
                    if (attr != null)
                    {
                        ret = attr.Value;
                    }
                }
            }

            return ret;
        }
        private string GetFile(string folder, string ext)
        {
            string[] files = Directory.GetFiles(folder, ext, SearchOption.AllDirectories);
            if (files != null && files.Length > 0)
            {
                return files[0];
            }
            return "";
        }
        private void RunCommand(string exe, string args, Action<bool> done)
        {
            //unbundle
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = exe;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = args;

            log.Info("RunCommand Started: =======");
            log.Info("Command Name: " + exe);
            log.Info("Command Parameters: " + args);
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            UpdateStatus("Running command " + Path.GetFileNameWithoutExtension(exe));
            try
            {
                // Start the process with the info we specified.
                // Call WaitForExit and then the using statement will close.
                using (Process exeProcess = Process.Start(startInfo))
                {
                    exeProcess.ErrorDataReceived += (s, e) => log.Error(e.Data);
                    exeProcess.OutputDataReceived += (s, e) => log.Info(e.Data);
                    exeProcess.BeginErrorReadLine();
                    exeProcess.BeginOutputReadLine();

                    exeProcess.WaitForExit();
                    //try
                    //{
                    //    log.Info(exeProcess.StandardOutput.ReadToEnd());
                    //    log.Error(exeProcess.StandardError.ReadToEnd());
                    //}
                    //catch { }
                    log.Info("RunCommand End: =======");
                    UpdateStatus("Command " + Path.GetFileNameWithoutExtension(exe) + " finished successfully");
                    done(true);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Command " + Path.GetFileNameWithoutExtension(exe) + " Failed");
                log.Info("Exception happens in " + Path.GetFileNameWithoutExtension(exe) + " " + ex);
                done(false);
            }
        }

        private void ConvertCerToPfx(string cer, string pfx)
        {
            X509Certificate cert = new X509Certificate(cer);

            byte[] certData = cert.Export(X509ContentType.Pkcs12, "!cbiatwt2");

            System.IO.File.WriteAllBytes(pfx, certData);
        }
        private void GetCerticationInfo(string cerFile, out string publisher, out string codingSign)
        {
            publisher = "CN=Siemens";
            codingSign = "";


            X509Certificate2 c = new X509Certificate2(cerFile);
            
            publisher = c.Issuer;
            foreach(X509Extension ext in c.Extensions)
            {
                if(ext is X509EnhancedKeyUsageExtension)
                {
                    OidCollection oids = ((X509EnhancedKeyUsageExtension)ext).EnhancedKeyUsages;
                    foreach (Oid oid in oids)
                    {
                        codingSign = oid.Value;
                        break;
                    }
                    break;
                }
            }
            if(string.IsNullOrWhiteSpace(codingSign))
            {
                throw new FormatException("Can't read Enhanced Key Usage from cerfication file: " + cerFile);
            }
        }

        private void browseButton_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.SelectedPath = "";
            folderBrowserDialog1.ShowDialog();
            appxLocationText.Text = folderBrowserDialog1.SelectedPath;
        }
        void UpdateStatus(string txt)
        {
            statusLabel.Text = "Status: " + txt;
        }
    }
}
