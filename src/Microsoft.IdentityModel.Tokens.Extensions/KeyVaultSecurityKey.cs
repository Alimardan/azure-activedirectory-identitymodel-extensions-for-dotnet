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

using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.IdentityModel.Logging;

namespace Microsoft.IdentityModel.Tokens.Extensions
{
    /// <summary>
    /// Provides signing and verifying operations using Azure Key Vault.
    /// </summary>
    public class KeyVaultSecurityKey : SecurityKey
    {
        private byte[] _symmetricKey;
        private int? _keySize;
        private string _keyId;

        /// <summary>
        /// The authentication callback delegate which is to be implemented by the client code.
        /// </summary>
        /// <param name="authority">Identifier of the authority, a URL.</param>
        /// <param name="resource">Identifier of the target resource that is the recipient of the requested token, a URL.</param>
        /// <param name="scope">The scope of the authentication request.</param>
        /// <returns>An access token for Azure Key Vault.</returns>
        public delegate Task<string> AuthenticationCallback(string authority, string resource, string scope);

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyVaultSecurityKey"/> class.
        /// </summary>
        /// <param name="keyIdentifier">The key identifier.</param>
        public KeyVaultSecurityKey(string keyIdentifier)
            : this(keyIdentifier, new AuthenticationCallback((new AzureServiceTokenProvider()).KeyVaultTokenCallback))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyVaultSecurityKey"/> class.
        /// </summary>
        /// <param name="keyIdentifier">The key identifier.</param>
        /// <param name="clientId">Identifier of the client.</param>
        /// <param name="clientSecret">Secret of the client identity.</param>
        public KeyVaultSecurityKey(string keyIdentifier, string clientId, string clientSecret)
            : this(keyIdentifier, new AuthenticationCallback(async (string authority, string resource, string scope) => (await (new AuthenticationContext(authority, TokenCache.DefaultShared)).AcquireTokenAsync(resource, new ClientCredential(clientId, clientSecret)).ConfigureAwait(false)).AccessToken))
        {
            if (string.IsNullOrEmpty(clientId))
                throw LogHelper.LogArgumentNullException(nameof(clientId));
            else if (string.IsNullOrEmpty(clientSecret))
                throw LogHelper.LogArgumentNullException(nameof(clientSecret));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyVaultSecurityKey"/> class.
        /// </summary>
        /// <param name="keyIdentifier">The key identifier.</param>
        /// <param name="callback">The authentication callback.</param>
        public KeyVaultSecurityKey(string keyIdentifier, AuthenticationCallback callback)
        {
            Callback = callback ?? throw LogHelper.LogArgumentNullException(nameof(callback));
            KeyId = keyIdentifier;
        }

        internal KeyVaultSecurityKey(string keyIdentifier, int keySize, byte[] symmetricKey)
        {
            _keyId = keyIdentifier;
            _keySize = keySize;
            _symmetricKey = symmetricKey;
        }

        /// <summary>
        /// The authentication callback delegate that retrieves an access token for the Key Vault.
        /// </summary>
        public AuthenticationCallback Callback { get; }

        /// <summary>
        /// The uniform resource identifier of the security key.
        /// </summary>
        public override string KeyId
        {
            get => _keyId;
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw LogHelper.LogArgumentNullException(nameof(value));
                else if (StringComparer.Ordinal.Equals(_keyId, value))
                    return;

                _keyId = value;

                // Reset the properties so they can be retrieved from Azure Key Vault the next time they are accessed.
                _keySize = null;
                _symmetricKey = null;
            }
        }

        /// <summary>
        /// The size of the security key.
        /// </summary>
        public override int KeySize
        {
            get
            {
                if (!_keySize.HasValue)
                    Initialize();

                return _keySize.Value;
            }
        }

        /// <summary>
        /// Gets the symmetric key from Azure Key Vault.
        /// </summary>
        public byte[] SymmetricKey
        {
            get
            {
                if (_symmetricKey == null)
                    Initialize();

                return _symmetricKey;
            }
        }

        /// <summary>
        /// Retrieve the properties from Azure Key Vault.
        /// </summary>
        private void Initialize()
        {
            using (var client = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(Callback)))
            {
                var bundle = client.GetKeyAsync(_keyId, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
                _keySize = new BitArray(bundle.Key.N).Length;
                _symmetricKey = bundle.Key.K;
            }
        }
    }
}
