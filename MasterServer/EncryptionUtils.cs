using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Shared;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MasterServer
{
    public class EncryptionUtils
    {
        public static X509Certificate2 GenerateCertificate(string certName, out AsymmetricCipherKeyPair rsaKeyPair)
        {
            var keypairgen = new RsaKeyPairGenerator();
            keypairgen.Init(new KeyGenerationParameters(new SecureRandom(new CryptoApiRandomGenerator()), 1024));

            rsaKeyPair = keypairgen.GenerateKeyPair();

            var gen = new X509V3CertificateGenerator();

            var CN = new X509Name("CN=" + certName);
            var SN = BigInteger.ProbablePrime(120, new Random());

            gen.SetSerialNumber(SN);
            gen.SetSubjectDN(CN);
            gen.SetIssuerDN(CN);
            gen.SetNotAfter(DateTime.MaxValue);
            gen.SetNotBefore(DateTime.Now.Subtract(new TimeSpan(7, 0, 0, 0)));
            gen.SetSignatureAlgorithm("MD5WithRSA");
            gen.SetPublicKey(rsaKeyPair.Public);

            var newCert = gen.Generate(rsaKeyPair.Private);

            return new X509Certificate2(DotNetUtilities.ToX509Certificate((Org.BouncyCastle.X509.X509Certificate)newCert));
        }
    }
}
