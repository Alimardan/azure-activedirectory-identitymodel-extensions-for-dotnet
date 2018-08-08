﻿//------------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.
// All rights reserved.
//
// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//------------------------------------------------------------------------------

#pragma warning disable CS3016 // Arrays as attribute arguments is not CLS-compliant

namespace Microsoft.IdentityModel.Tokens.Extensions.Tests
{
    using System;
    using System.Linq;
    using Microsoft.Azure.KeyVault;
    using Microsoft.IdentityModel.Tests;
    using Microsoft.IdentityModel.Tokens.Extensions;
    using Xunit;

    public class KeyVaultSignatureProviderTests
    {
        private readonly IKeyVaultClient _client;
        private readonly SecurityKey _key;

        public KeyVaultSignatureProviderTests()
        {
            _client = new MockKeyVaultClient();
            _key = new KeyVaultSecurityKey(KeyVaultUtilities.CreateKeyIdentifier(), keySize: default, symmetricKey: default);
        }

        public static TheoryData<SignatureProviderTheoryData> SignatureProviderTheoryData
        {
            get => new TheoryData<SignatureProviderTheoryData>
            {
                new SignatureProviderTheoryData
                {
                    Algorithm = null,
                    ExpectedException = ExpectedException.ArgumentNullException(),
                    First = true,
                    TestId = "NullAlgorithm",
                },
                new SignatureProviderTheoryData
                {
                    Algorithm = string.Empty,
                    ExpectedException = ExpectedException.ArgumentNullException(),
                    TestId = "EmptyAlgorithm",
                },
                new SignatureProviderTheoryData
                {
                    Algorithm = SecurityAlgorithms.RsaSha256,
                    ExpectedException = ExpectedException.NoExceptionExpected,
                    TestId = nameof(SecurityAlgorithms.RsaSha256),
                },
                new SignatureProviderTheoryData
                {
                    Algorithm = SecurityAlgorithms.RsaSha384,
                    ExpectedException = ExpectedException.NoExceptionExpected,
                    TestId = nameof(SecurityAlgorithms.RsaSha384),
                },
                new SignatureProviderTheoryData
                {
                    Algorithm = SecurityAlgorithms.RsaSha512,
                    ExpectedException = ExpectedException.NoExceptionExpected,
                    TestId = nameof(SecurityAlgorithms.RsaSha512),
                },
            };
        }

        [Theory, MemberData(nameof(SignatureProviderTheoryData))]
        public void SignatureTest(SignatureProviderTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.SignatureTest", theoryData);

            try
            {
                var provider = new KeyVaultSignatureProvider(_key, theoryData.Algorithm, willCreateSignatures: true, _client);
                Assert.NotNull(provider);

                var input = Guid.NewGuid().ToByteArray();
                var signature = provider.Sign(input);
                Assert.NotNull(signature);
                Assert.Equal(128, signature.Length);
                Assert.True(provider.Verify(input, signature));

                var tamperedInput = new byte[input.Length];
                input.CopyTo(tamperedInput, 0);
                if (tamperedInput[0] == byte.MaxValue)
                    tamperedInput[0]--;
                else
                    tamperedInput[0]++;

                Assert.False(provider.Verify(tamperedInput, signature));

                foreach (var data in SignatureProviderTheoryData)
                {
                    var newAlgorithm = (data.Single() as SignatureProviderTheoryData)?.Algorithm;
                    if (string.IsNullOrEmpty(newAlgorithm))
                        continue; // Skip invalid input

                    // Check that a given Security Key will only validate a signature using the same hash algorithm.
                    var isValidSignature = new KeyVaultSignatureProvider(_key, newAlgorithm, willCreateSignatures: false, _client).Verify(input, signature);
                    if (StringComparer.Ordinal.Equals(theoryData.Algorithm, newAlgorithm))
                        Assert.True(isValidSignature);
                    else
                        Assert.False(isValidSignature);
                }

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception exception)
            {
                theoryData.ExpectedException.ProcessException(exception, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }
    }
}

#pragma warning restore CS3016 // Arrays as attribute arguments is not CLS-compliant
