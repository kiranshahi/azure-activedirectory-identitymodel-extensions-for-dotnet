//------------------------------------------------------------------------------
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Logging;
using Newtonsoft.Json;

namespace Microsoft.IdentityModel.Tokens
{
    /// <summary>
    /// Contains a collection of <see cref="JsonWebKey"/> that can be populated from a json string.
    /// </summary>
    /// <remarks>provides support for http://tools.ietf.org/html/rfc7517.</remarks>
    [JsonObject]
    public class JsonWebKeySet
    {
        /// <summary>
        /// Returns a new instance of <see cref="JsonWebKeySet"/>.
        /// </summary>
        /// <param name="json">a string that contains JSON Web Key parameters in JSON format.</param>
        /// <returns><see cref="JsonWebKeySet"/></returns>
        /// <exception cref="ArgumentNullException">If 'json' is null or empty.</exception>
        /// <exception cref="ArgumentException">If 'json' fails to deserialize.</exception>
        public static JsonWebKeySet Create(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw LogHelper.LogArgumentNullException(nameof(json));

            return new JsonWebKeySet(json);
        }

        /// <summary>
        /// Initializes an new instance of <see cref="JsonWebKeySet"/>.
        /// </summary>
        public JsonWebKeySet()
        {
        }

        /// <summary>
        /// Initializes an new instance of <see cref="JsonWebKeySet"/> from a json string.
        /// </summary>
        /// <param name="json">a json string containing values.</param>
        /// <exception cref="ArgumentNullException">If 'json' is null or empty.</exception>
        /// <exception cref="ArgumentException">If 'json' fails to deserialize.</exception>
        public JsonWebKeySet(string json) : this(json, null)
        {
        }

        /// <summary>
        /// Initializes an new instance of <see cref="JsonWebKeySet"/> from a json string.
        /// </summary>
        /// <param name="json">a json string containing values.</param>
        /// <param name="jsonSerializerSettings">jsonSerializerSettings</param>
        /// <exception cref="ArgumentNullException">If 'json' is null or empty.</exception>
        /// <exception cref="ArgumentException">If 'json' fails to deserialize.</exception>
        public JsonWebKeySet(string json, JsonSerializerSettings jsonSerializerSettings)
        {
            if (string.IsNullOrEmpty(json))
                throw LogHelper.LogArgumentNullException(nameof(json));

            try
            {
                LogHelper.LogVerbose(LogMessages.IDX10806, json, this);
                if (jsonSerializerSettings != null)
                {
                    JsonConvert.PopulateObject(json, this, jsonSerializerSettings);
                }
                else
                {
                    JsonConvert.PopulateObject(json, this);
                }
            }
            catch (Exception ex)
            {
                throw LogHelper.LogExceptionMessage(new ArgumentException(LogHelper.FormatInvariant(LogMessages.IDX10805, json, GetType()), ex));
            }
        }

        /// <summary>
        /// When deserializing from JSON any properties that are not defined will be placed here.
        /// </summary>
        [JsonExtensionData]
        public virtual IDictionary<string, object> AdditionalData { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets the <see cref="IList{JsonWebKey}"/>.
        /// </summary>       
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, PropertyName = JsonWebKeySetParameterNames.Keys, Required = Required.Default)]
        public IList<JsonWebKey> Keys { get; private set; } = new List<JsonWebKey>();

        /// <summary>
        /// Default value for the flag that controls whether unresolved JsonWebKeys will be included in the resulting collection of <see cref="GetSigningKeys"/> method.
        /// </summary>
        [DefaultValue(true)]
        public static bool DefaultSkipUnresolvedJsonWebKeys = true;

        /// <summary>
        /// Flag that controls whether unresolved JsonWebKeys will be included in the resulting collection of <see cref="GetSigningKeys"/> method.
        /// </summary>
        [DefaultValue(true)]
        public bool SkipUnresolvedJsonWebKeys { get; set; } = DefaultSkipUnresolvedJsonWebKeys;

        /// <summary>
        /// Returns the JsonWebKeys as a <see cref="IList{SecurityKey}"/>.
        /// </summary>
        /// <remarks>
        /// To include unresolved JsonWebKeys in the resulting <see cref="SecurityKey"/> collection, set <see cref="SkipUnresolvedJsonWebKeys"/> to <c>false</c>.
        /// </remarks>
        public IList<SecurityKey> GetSigningKeys()
        {
            var signingKeys = new List<SecurityKey>();
            foreach (var webKey in Keys)
            {
                // skip if "use" (Public Key Use) parameter is not empty or "sig"
                // https://tools.ietf.org/html/rfc7517#section-4.2
                if (!(string.IsNullOrWhiteSpace(webKey.Use) || webKey.Use.Equals(JsonWebKeyUseNames.Sig, StringComparison.Ordinal)))
                {
                    LogHelper.LogInformation(LogHelper.FormatInvariant(LogMessages.IDX10808, webKey.KeyId ?? "null" , webKey.Use ?? "null"));

                    if (!SkipUnresolvedJsonWebKeys)
                        signingKeys.Add(webKey);

                    continue;
                }

                var isResolved = false;
                if (webKey.Kty != null)
                {
                    if (webKey.Kty.Equals(JsonWebAlgorithmsKeyTypes.RSA, StringComparison.Ordinal))
                    {
                        if (JsonWebKeyConverter.TryConvertToX509SecurityKey(webKey, out SecurityKey securityKey))
                        {
                            signingKeys.Add(webKey);
                            isResolved = true;
                        }

                        if (JsonWebKeyConverter.TryCreateRsaSecurityKey(webKey, out securityKey))
                        {
                            signingKeys.Add(securityKey);
                            isResolved = true;
                        }
                    }
                    else if (webKey.Kty.Equals(JsonWebAlgorithmsKeyTypes.EllipticCurve, StringComparison.Ordinal))
                    {
                        if (JsonWebKeyConverter.TryConvertECDsaSecurityKey(webKey, out SecurityKey securityKey))
                        {
                            signingKeys.Add(securityKey);
                            isResolved = true;
                        }
                    }
                    else
                    {
                        // kty is not 'EC' or 'RSA'
                        LogHelper.LogInformation(LogHelper.FormatInvariant(LogMessages.IDX10809, webKey.Kty ?? "null"));

                        if (!SkipUnresolvedJsonWebKeys)
                            signingKeys.Add(webKey);
                    }
                }

                if (!isResolved)
                {
                    LogHelper.LogInformation(LogHelper.FormatInvariant(LogMessages.IDX10810, webKey.KeyId ?? "null", webKey.Kty ?? "null"));

                    if (!SkipUnresolvedJsonWebKeys)
                        signingKeys.Add(webKey);
                }
            }

            return signingKeys;
        }
    }
}
