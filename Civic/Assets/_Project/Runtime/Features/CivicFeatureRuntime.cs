using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civic.Features
{
    public static class CivicFeatureRuntime
    {
        private const string CommandLineArgument = "-civicFeatures";
        private static CivicFeatureResolution currentResolution = CivicFeatureResolver.Resolve(Array.Empty<string>());

        public static CivicFeatureResolution Current => currentResolution;
        public static bool IsRunLocked { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ResetForMainMenu();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyCommandLineSelection()
        {
            var requested = ParseCommandLine(Environment.GetCommandLineArgs());
            if (requested.Count > 0)
            {
                SetPending(requested);
            }
        }

        public static CivicFeatureResolution Preview(IEnumerable<string> requestedIds)
        {
            return CivicFeatureResolver.Resolve(requestedIds);
        }

        public static CivicFeatureResolution SetPending(IEnumerable<string> requestedIds)
        {
            if (IsRunLocked)
            {
                throw new InvalidOperationException("런 시작 후에는 기능 구성을 변경할 수 없습니다.");
            }

            currentResolution = CivicFeatureResolver.Resolve(requestedIds);
            return currentResolution;
        }

        public static CivicFeatureResolution BeginRun()
        {
            if (!currentResolution.IsValid)
            {
                throw new InvalidOperationException(string.Join("\n", currentResolution.Errors));
            }

            IsRunLocked = true;
            return currentResolution;
        }

        public static CivicFeatureResolution EnsureRunStarted()
        {
            return IsRunLocked ? currentResolution : BeginRun();
        }

        public static void ResetForMainMenu()
        {
            IsRunLocked = false;
            currentResolution = CivicFeatureResolver.Resolve(Array.Empty<string>());
        }

        public static CivicFeatureResolution ConfigureAndBeginForTests(IEnumerable<string> requestedIds)
        {
            ResetForMainMenu();
            SetPending(requestedIds);
            return BeginRun();
        }

        public static IReadOnlyList<string> ParseCommandLine(IReadOnlyList<string> arguments)
        {
            if (arguments == null)
            {
                return Array.Empty<string>();
            }

            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index] ?? string.Empty;
                if (argument.StartsWith(CommandLineArgument + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseIds(argument.Substring(CommandLineArgument.Length + 1));
                }

                if (string.Equals(argument, CommandLineArgument, StringComparison.OrdinalIgnoreCase) && index + 1 < arguments.Count)
                {
                    return ParseIds(arguments[index + 1]);
                }
            }

            return Array.Empty<string>();
        }

        private static IReadOnlyList<string> ParseIds(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
