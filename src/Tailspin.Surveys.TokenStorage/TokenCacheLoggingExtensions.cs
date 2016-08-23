﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Tailspin.Surveys.TokenStorage
{
    /// <summary>
    /// ILogger extensions for events which occur in the RedisTokenCache
    /// </summary>
    public static class TokenCacheLoggingExtensions
    {
        public static void ReadFromCacheFailed(this ILogger logger, Exception exp)
        {
            logger.LogError("Reading from cache failed", exp);
        }
        public static void WriteToCacheFailed(this ILogger logger, Exception exp)
        {
            logger.LogError("Writing to cache failed", exp);
        }
        public static void TokenCacheCleared(this ILogger logger, string userId)
        {
            logger.LogInformation("Cleared token cache for User: {0}", userId);
        }
        public static void TokensRetrievedFromStore(this ILogger logger, string key)
        {
            //https://github.com/aspnet/Logging/commit/7a05d121d6c120a15d5a2178e3e45d7c400d22c0
            //logger.LogVerbose("Retrieved all tokens from store for Key: {0}", key);
            logger.LogTrace("Retrieved all tokens from store for Key: {0}", key);
        }
        public static void TokensWrittenToStore(this ILogger logger, string clientId, string userId, string resource)
        {
            //https://github.com/aspnet/Logging/commit/7a05d121d6c120a15d5a2178e3e45d7c400d22c0
            //logger.LogVerbose("Token states changed for Client: {0} User: {1}  Resource: {2} writing all tokens back to store", clientId, userId, resource);
            logger.LogTrace("Token states changed for Client: {0} User: {1}  Resource: {2} writing all tokens back to store", clientId, userId, resource);
        }
    }
}
