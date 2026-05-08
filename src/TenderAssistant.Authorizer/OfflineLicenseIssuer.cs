using System.Security.Cryptography;
using System.Text;
using TenderAssistant.Licensing;

namespace TenderAssistant.Authorizer;

internal static class OfflineLicenseIssuer
{
    private const string PrivateKeyPem = """
-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDAHQORFvIJGrv4
zZPeoTK3cwU5kTqfE5hVvKURD9ApM5cBtDd7qP2+8bFZFiMRRSKvDkaB84Pq1R5g
sOuNsRB/KLw0CSY45HrPIphCbrIOzMOJ1UUKdcQ4h6dQ+ixPeIZ0DG2w+gxy2112
i6ULZhH8Hb0cWYm1vUF0uMp/sLQtbmvIzLdp9i+Lr4Auebm56sIUb4ihmPUyWu+h
kJjIOkKrcCSQguSZChh4PAJOnupawDZF1XlU+dz2/E79UU981jBX8ncD/1b1WiDu
Sqbjqp7+xYnTMHlDZLckiNif7BF9t9RXaLOwHR73g3oiE1S71Hz5D+dVwQXiPK3I
FwEKg4EfAgMBAAECggEAB6xhAGzpMqHlu39X66mGoWXaYefsw9PViX8lHLRZUnv5
XnEN/PDNXyYU465gV4fqZbxCkV2M+avqMF8rWW0mxTLMVEu/7L/Q2UXUImXSKN3W
5Zd7es/PYZlRfojaN5EPKxgf/X9PUKXLFY69reHgJVt5ZTt8+6Gujp9JlKYV3Eae
DGZo7OtPwENusW+rBMXRAjlA3UDf3tuvVL7YFY60PBTSLSQi2admrFanweZRlmDT
iTK1nDmVoC7dlc4kXHIEak9/hYgH9r0NVQxq2CSKH7pwbbML4LutFX++uzM8MFbL
79SW7yemr2KqSREotu9JTIi1yoWqSuckd6VngcoGQQKBgQDt41UGb5xg84Mjfl3W
BxNpu/FWmGtQLb+ZKsYjDmHvWcdpd4i4FjYZO1Bl4532SMIhKqa4u7e/ySLpTHtX
wv0QI27iZuila7IJaeM06cfEGlzZSwXhwjedwp+WfWT5XXDtOlEXeJeIhgrOqii0
gGcKzUbzDNdq7M8o9gTwxuWJIQKBgQDOvX0rMSi+wqHBXdOKVY8YL9lVa+GL4LGN
QOM4G28/IOsLbNtFVuSGe+9JCmfJyVJLSIlUxH+DNEF51iffajRRoDJs57gK8lzx
uaFRpfirmPPOisKsx9yhTZ5jdNfvMNaz3zNPTuc8woLGa+BpQtPRDT0jTW6Hkmoo
U0Xhh+SCPwKBgE+yTZXuZnGEo3aMq1s826AcuunL/ofKC9qAngi4lM7fQRNwXHlv
14f0eybnbtBH5+G8rEZPfWvfMrb+TIRGawmxFUD8QQzKW8cTlm7vs2Fbg6e4nqvX
qVJNFbIRKHbyexa+5tP6LqoqXgyGrURrkBnqU86xiqnj1DNg2J7hw5yBAoGAOMNa
PKOwtj+mPftO+6pmMZPhrkyCju9QkKICQQN3VfPp1Sc8RRuIf0xD9OAAgyzdhYIT
As043YNZfuRH6lW0q7y6W6B7rbXBwzTekBZr4mGKf2kl7l6puzgehtwr9aaJLLXZ
1qqpXsthMK2p3fzVP47M/IZkFKEkuJG0nCm9me0CgYEAkEDAQm885bttv4LUnuAf
Z8Vhxl8MMIu1kFq3RLt16Izof6NdtCwtr7Apad2LcpgNA3GnqLbNTxFTGYaCRgBh
Sw8mWUhN6TkeQY7DG/sRiOaehm7ii6R51Ybo2vpRTGrCfUgA93yWeejZupy2hOeJ
Blb1nG1tq0aPXVV5q4v/Pzg=
-----END PRIVATE KEY-----
""";

    public static LicenseEnvelope Sign(LicensePayload payload)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(PrivateKeyPem);

        var data = Encoding.UTF8.GetBytes(OfflineLicenseJson.SerializePayload(payload));
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return new LicenseEnvelope(payload, Convert.ToBase64String(signature), OfflineLicenseCrypto.Algorithm, OfflineLicenseCrypto.KeyId);
    }
}
