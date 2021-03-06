﻿/**
 * Google Drive Sync for KeePass Password Safe
 * Copyright(C) 2020       Walter Goodwin
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
**/

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace GenVerInfo
{
    class Program
    {
        static void Main(string[] args)
        {
            // For info see "Signing" paragraph here:
            // https://keepass.info/help/v2_dev/plg_index.html#upd

            // Two modes of operation:
            // 1. If no parameters are passed, a new RSA key pair in XML format
            // is printed to the console.  Use this data in PrivateKey.xml (in 
            // this project) and PubKey.xml (in GoogleDriveSync project) to
            // sign a version info file in Mode 2.
            //
            // 2. Create a signed version info file, by specifying:
            //   a) The path to the plugin assembly.
            //   b) The path to the output file.
            //   

            if (args.Length == 0)
            {
                using (RSACryptoServiceProvider rsa
                    = new RSACryptoServiceProvider(4096))
                {
                    rsa.PersistKeyInCsp = false;
                    Console.WriteLine("Private key: " + rsa.ToXmlString(true));
                    Console.WriteLine("Public key: " + rsa.ToXmlString(false));
                }
                return;
            }
            else if (args.Length != 2)
            {
                throw new ArgumentException(
                    "Expected one input assembly file and one output file " +
                    "path");
            }
            string input = args[0];
            if (!File.Exists(input))
            {
                throw new ArgumentException(
                    string.Format("Can't access file '{0}'.", input));
            }
            Assembly asm = Assembly.LoadFrom(input);
            AssemblyTitleAttribute titleAttr
                = asm.GetCustomAttribute<AssemblyTitleAttribute>();
            AssemblyFileVersionAttribute verAttr
                = asm.GetCustomAttribute<AssemblyFileVersionAttribute>();
            if (titleAttr == null || string.IsNullOrEmpty(titleAttr.Title) ||
                verAttr == null || string.IsNullOrEmpty(verAttr.Version) ||
                !Version.TryParse(verAttr.Version, out Version asmVer) ||
                asmVer.Major == -1 || asmVer.Minor == -1 ||
                asmVer.Build == -1)
            {
                throw new ArgumentException(
                    "The assembly is missing some version info.");
            }
            StringBuilder verinfoLine = new StringBuilder(titleAttr.Title);
            verinfoLine.Append(':');
            verinfoLine.Append(verAttr.Version);

            byte[] hash;
            byte[] material = Encoding.UTF8.GetBytes(verinfoLine.ToString() + '\n');
            using (SHA512CryptoServiceProvider hasher
                    = new SHA512CryptoServiceProvider())
            {
                using (RSACryptoServiceProvider signer
                        = new RSACryptoServiceProvider())
                {
                    signer.FromXmlString(Resources.PrivateKey);
                    hash = signer.SignData(material.ToArray(), hasher);
                }
            }

            using (FileStream outFile
                = new FileStream(args[1], FileMode.Create))
            //using (GZipStream zipper 
            //    = new GZipStream(outFile, CompressionLevel.Optimal))
            using (StreamWriter wtr
                = new StreamWriter(outFile, Encoding.UTF8))
            {
                wtr.Write(':');
                wtr.Write(Convert.ToBase64String(hash));
                wtr.Write(Environment.NewLine);
                wtr.Write(verinfoLine.ToString());
                wtr.Write(Environment.NewLine);
                wtr.Write(':');
            }
        }
    }
}
