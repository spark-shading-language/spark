﻿// Copyright 2011 Intel Corporation
// All Rights Reserved
//
// Permission is granted to use, copy, distribute and prepare derivative works of this
// software for any purpose and without fee, provided, that the above copyright notice
// and this statement appear in all copies.  Intel makes no representations about the
// suitability of this software for any purpose.  THIS SOFTWARE IS PROVIDED "AS IS."
// INTEL SPECIFICALLY DISCLAIMS ALL WARRANTIES, EXPRESS OR IMPLIED, AND ALL LIABILITY,
// INCLUDING CONSEQUENTIAL AND OTHER INDIRECT DAMAGES, FOR THE USE OF THIS SOFTWARE,
// INCLUDING LIABILITY FOR INFRINGEMENT OF ANY PROPRIETARY RIGHTS, AND INCLUDING THE
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.  Intel does not
// assume any responsibility for any errors which may appear in this software nor any
// responsibility to update it.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spark
{
    public static class Utilities
    {
        public static V Cache<K, V>(
            this IDictionary<K, V> dictionary,
            K key,
            Func<V> generator)
        {
            V result;
            if (dictionary.TryGetValue(key, out result))
                return result;

            result = generator();
            dictionary[key] = result;
            return result;
        }

        public static T[] Eager<T>(this IEnumerable<T> sequence)
        {
            if (sequence is T[])
                return (T[])sequence;

            return sequence.ToArray();
        }

        public static IEnumerable<T> Separate<T>(
            this IEnumerable<T> seq,
            T val)
        {
            bool first = true;
            foreach (var t in seq)
            {
                if (!first)
                    yield return val;
                first = false;

                yield return t;
            }
        }

        public static string Concat(
            this IEnumerable<string> seq)
        {
            var builder = new StringBuilder();
            foreach (var s in seq)
                builder.Append(s);
            return builder.ToString();
        }
    }

}