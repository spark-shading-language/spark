// Copyright 2011 Intel Corporation
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

namespace Spark.Resolve
{
    public interface IRes<out T> : IEnumerable<T>
    {
        bool HasValue { get; }
        T Value { get; }
    }

    public class Res<T> : IRes<T>
    {
        public Res(
            T value)
        {
            _hasValue = true;
            _value = value;
        }

        public Res()
        {
        }

        public bool HasValue { get { return _hasValue; } }
        public T Value { get { return _value; } }

        public IEnumerator<T> GetEnumerator()
        {
            if (_hasValue)
                yield return _value;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            if (_hasValue)
                yield return _value;
        }

        private bool _hasValue;
        private T _value;
    }

    public static class Res
    {
        public static IRes<T> Value<T>(T value)
        {
            return new Res<T>(value);
        }

        public static IRes<R> Select<T, R>(
            this IRes<T> res,
            Func<T, R> f)
        {
            if (!res.HasValue)
                return new Res<R>();

            return new Res<R>(
                f(res.Value));
        }

        public static IRes<R> Select<T, R>(
            this IRes<T> res,
            Func<T, IRes<R>> f)
        {
            if (!res.HasValue)
                return new Res<R>();

            var fRes = f(res.Value);
            if (!fRes.HasValue)
                return new Res<R>();

            return new Res<R>(
                fRes.Value );
        }

        public static IRes<R> Select<A, B, R>(
            this IRes<Tuple<A, B>> res,
            Func<A, B, R> f)
        {
            if (!res.HasValue)
                return new Res<R>();
            return new Res<R>(
                f(res.Value.Item1, res.Value.Item2));
        }

        public static IRes<R> SelectMany<T, U, R>(
            this IRes<T> res,
            Func<T, IRes<U>> f,
            Func<T, U, IRes<R>> g)
        {
            if (!res.HasValue)
                return new Res<R>();

            var fRes = f(res.Value);
            if (!fRes.HasValue)
                return new Res<R>();

            var gRes = g(res.Value, fRes.Value);
            if (!gRes.HasValue)
                return new Res<R>();

            return new Res<R>(
                gRes.Value);
        }

        public static IRes<R> SelectMany<T, U, R>(
            this IRes<T> res,
            Func<T, IRes<U>> f,
            Func<T, U, R> g)
        {
            if (!res.HasValue)
                return new Res<R>();

            var fRes = f(res.Value);

            if (!fRes.HasValue)
                return new Res<R>();

            var gVal = g(res.Value, fRes.Value);

            return new Res<R>(
                gVal );
        }

        public static IRes<T[]> Flatten<T>(
            this IEnumerable<IRes<T>> results)
        {
            if (results.Any((res) => !res.HasValue))
            {
                // Just return the aggregated diagnostics
                return new Res<T[]>();
            }

            // If there were no problems, then we
            // can aggregate the values into an array
            var values = from res in results
                         select res.Value;

            return new Res<T[]>(
                values.ToArray());
        }

        public static IRes<Tuple<A, B>> Zip<A, B>(
            IRes<A> a,
            IRes<B> b)
        {
            if (!a.HasValue || !b.HasValue)
                return new Res<Tuple<A, B>>();

            return new Res<Tuple<A, B>>(
                new Tuple<A, B>(a.Value, b.Value));
        }

        public static IRes<R> Zip<A, B, R>(
            IRes<A> a,
            IRes<B> b,
            Func<A, B, R> f)
        {
            if (!a.HasValue || !b.HasValue)
                return new Res<R>();

            return new Res<R>(f(a.Value, b.Value));
        }

        public static IRes<T> Flatten<T>(
            this IRes<IRes<T>> res)
        {
            if (!res.HasValue)
                return new Res<T>();

            var inner = res.Value;
            if (!inner.HasValue)
                return new Res<T>();

            return new Res<T>(
                inner.Value);
        }

        public static IRes<T> Error<T>()
        {
            return new Res<T>();
        }
    }

}
