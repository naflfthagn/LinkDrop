using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;

namespace LinkDrop
{
    internal sealed class DeviceIdentity
    {
        public string Fingerprint { get; private set; }
        public string ShortId { get; private set; }
        public string SecurityCode { get; private set; }
        public X509Certificate2 Certificate { get; private set; }

        private DeviceIdentity() { }

        public static DeviceIdentity LoadOrCreate()
        {
            EnsureDirectories();
            try
            {
                X509Certificate2 loaded = LoadExisting();
                if (loaded != null)
                {
                    return Build(loaded);
                }
            }
            catch (Exception ex)
            {
                Log.Error("现有身份证书无法读取，将重新生成", ex);
                TryArchiveBrokenIdentity();
            }

            string generatedPassword;
            X509Certificate2 generated = Generate(out generatedPassword);
            Save(generated, generatedPassword);
            return Build(generated);
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(AppPaths.BaseDir);
        }

        private static X509Certificate2 LoadExisting()
        {
            if (!File.Exists(AppPaths.IdentityPfx) || !File.Exists(AppPaths.IdentityPassword)) return null;
            string protectedPassword = File.ReadAllText(AppPaths.IdentityPassword, Encoding.UTF8);
            string password = Dpapi.UnprotectString(protectedPassword);
            X509Certificate2 cert = new X509Certificate2(AppPaths.IdentityPfx, password,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
            if (!cert.HasPrivateKey) throw new CryptographicException("证书缺少私钥");
            if (cert.NotAfter < DateTime.Now.AddDays(30)) throw new CryptographicException("证书即将过期");
            return cert;
        }

        private static X509Certificate2 Generate(out string password)
        {
            SecureRandom random = new SecureRandom();
            RsaKeyPairGenerator generator = new RsaKeyPairGenerator();
            generator.Init(new KeyGenerationParameters(random, 2048));
            AsymmetricCipherKeyPair keyPair = generator.GenerateKeyPair();

            X509V3CertificateGenerator certGen = new X509V3CertificateGenerator();
            certGen.SetSerialNumber(BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), random));
            certGen.SetIssuerDN(new X509Name("CN=LinkDrop Local Device,O=QingChuan"));
            certGen.SetSubjectDN(new X509Name("CN=LinkDrop Local Device,O=QingChuan"));
            certGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            certGen.SetNotAfter(DateTime.UtcNow.AddYears(10));
            certGen.SetPublicKey(keyPair.Public);
            certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
            certGen.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));
            certGen.AddExtension(X509Extensions.ExtendedKeyUsage, false, new ExtendedKeyUsage(KeyPurposeID.IdKPServerAuth));
            Org.BouncyCastle.X509.X509Certificate bouncyCert = certGen.Generate(new Asn1SignatureFactory("SHA256WITHRSA", keyPair.Private, random));

            Pkcs12Store store = new Pkcs12StoreBuilder().Build();
            store.SetKeyEntry("linkdrop", new AsymmetricKeyEntry(keyPair.Private),
                new X509CertificateEntry[] { new X509CertificateEntry(bouncyCert) });
            byte[] passwordBytes = new byte[24];
            random.NextBytes(passwordBytes);
            password = Convert.ToBase64String(passwordBytes);
            using (MemoryStream memory = new MemoryStream())
            {
                store.Save(memory, password.ToCharArray(), random);
                X509Certificate2 cert = new X509Certificate2(memory.ToArray(), password,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
                cert.FriendlyName = "LinkDrop Local Device";
                return cert;
            }
        }

        private static void Save(X509Certificate2 cert, string password)
        {
            if (string.IsNullOrEmpty(password)) password = Convert.ToBase64String(RandomBytes(24));
            byte[] pfx = cert.Export(X509ContentType.Pfx, password);
            string tempPfx = AppPaths.IdentityPfx + ".tmp";
            File.WriteAllBytes(tempPfx, pfx);
            if (File.Exists(AppPaths.IdentityPfx)) File.Delete(AppPaths.IdentityPfx);
            File.Move(tempPfx, AppPaths.IdentityPfx);

            string protectedPassword = Dpapi.ProtectString(password);
            File.WriteAllText(AppPaths.IdentityPassword, protectedPassword, Encoding.UTF8);
        }

        private static byte[] RandomBytes(int count)
        {
            byte[] bytes = new byte[count];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return bytes;
        }

        private static void TryArchiveBrokenIdentity()
        {
            try
            {
                string suffix = ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                if (File.Exists(AppPaths.IdentityPfx)) File.Move(AppPaths.IdentityPfx, AppPaths.IdentityPfx + suffix);
                if (File.Exists(AppPaths.IdentityPassword)) File.Move(AppPaths.IdentityPassword, AppPaths.IdentityPassword + suffix);
            }
            catch { }
        }

        private static DeviceIdentity Build(X509Certificate2 cert)
        {
            DeviceIdentity identity = new DeviceIdentity();
            identity.Certificate = cert;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(cert.RawData);
                identity.Fingerprint = CryptoUtil.ToHex(hash);
            }
            identity.ShortId = identity.Fingerprint.Substring(0, 16);
            identity.SecurityCode = LinkDrop.SecurityCode.FromFingerprint(identity.Fingerprint);
            return identity;
        }
    }
}
