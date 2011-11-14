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

namespace Spark
{
    public static class Lazy
    {
        public static ILazy<T> New<T>(
            this ILazyFactory factory,
            Func<T> generator)
        {
            return new Lazy<T>(factory, generator);
        }

        public static ILazy<T> Value<T>(T value)
        {
            return new Lazy<T>(value);
        }
    }

    public interface ILazy
    {
        void Force();
    }

    public interface ILazy<out T> : ILazy
    {
        T Value { get; }
    }

    public class Lazy<T> : ILazy<T>
    {
        public Lazy(
            ILazyFactory factory,
            Func<T> generator)
        {
            if (factory == null)
                throw new NullReferenceException("factory");
            factory.Add(this);
            _generator = generator;
        }

        public Lazy(
            T value)
        {
            _value = value;
        }

        public T Value
        {
            get
            {
                if (_generator != null)
                {
                    Force();
                }
                return _value;
            }
        }

        public void Force()
        {
            if (_generator != null)
            {
                lock (this)
                {
                    if (_generator != null)
                    {
                        _value = _generator();
                        _generator = null;
                    }
                }
            }
        }


        private Func<T> _generator;
        private T _value;
    }

    public interface ILazyFactory
    {
        void Add(ILazy instance);
    }

    public class LazyFactory : ILazyFactory
    {
        public Lazy<T> Lazy<T>(Func<T> generator)
        {
            var result = new Lazy<T>(this, generator);
            Add(result);
            return result;
        }

        public void Force()
        {
            while (true)
            {
                if (_instances.Count == 0)
                    return;

                var oldInstances = _instances;
                _instances = new List<ILazy>();

                foreach (var instance in oldInstances)
                    instance.Force();
            }
        }

        public void Add(ILazy instance)
        {
            _instances.Add(instance);
        }

        private List<ILazy> _instances = new List<ILazy>();
    }


    public interface IBuilder : ILazyFactory
    {
        void DoneBuilding();

        void Force(); // Make the builder available
        void ForceDeep(); // Make the builder and all children available

        void AddBuildAction(Action action);
        void AddPostAction(Action action);
        void AddChild(IBuilder builder);

        ILazy<T> NewLazy<T>(Func<T> generator);
    }

    public class Builder : IBuilder
    {
        private enum BuilderState
        {
            Building,   // Actively adding new state/data
            AutoDone,
            Latent,     // Done adding new state/data, but latent actions remain
            Finalizing, // In process of performing latent actions
            Finalized,  // Done processing latent actions, ready to use
        }

        public Builder(
            IBuilder parent)
        {
            _parent = parent;
            if (_parent != null)
                _parent.AddChild(this);
        }

        public void DoneBuilding()
        {
            AssertBuildable();
            _state = BuilderState.Latent;
        }

        public void AutoDoneBuilding()
        {
            AssertBuildable();
            _state = BuilderState.AutoDone;
        }

        protected bool IsBuildable()
        {
            switch (_state)
            {
                case BuilderState.Building:
                case BuilderState.AutoDone:
                    return true;
                case BuilderState.Latent:
                case BuilderState.Finalizing:
                case BuilderState.Finalized:
                default:
                    return false;
            }
        }

        public void Force()
        {
            if (_state == BuilderState.Building && _parent != null)
                _parent.Force();

            switch (_state)
            {
                case BuilderState.Building:
                    throw new Exception("Builder not yet available");
                case BuilderState.AutoDone:
                case BuilderState.Latent:
                    ForceImpl();
                    return;
                case BuilderState.Finalizing:
                    throw new Exception("Circular reference in Builder");
                case BuilderState.Finalized:
                    return;
            }
        }

        public void ForceDeep()
        {
            Force();

            if( _children != null )
            {
                foreach( var c in _children )
                    c.ForceDeep();
            }
        }

        private void ForceImpl()
        {
            _state = BuilderState.Finalizing;
            while (_buildActions != null)
            {
                var actions = _buildActions;
                _buildActions = null;

                foreach (var a in actions)
                    a();
            }
            _state = BuilderState.Finalized;
            while (_postActions != null)
            {
                var actions = _postActions;
                _postActions = null;

                foreach (var a in actions)
                    a();
            }
        }

        public void AddBuildAction(Action action)
        {
            AssertBuildable();
            if (_buildActions == null)
                _buildActions = new List<Action>();
            _buildActions.Add(action);
        }

        public void AddPostAction(Action action)
        {
            if (_postActions == null)
                _postActions = new List<Action>();
            _postActions.Add(action);
        }

        public void AddChild(IBuilder child)
        {
            AssertBuildable();
            if (_children == null)
                _children = new List<IBuilder>();
            _children.Add(child);
        }

        protected void AssertBuildable()
        {
            switch (_state)
            {
                case BuilderState.Building:
                case BuilderState.AutoDone:
//                case BuilderState.Latent:
                case BuilderState.Finalizing:
                    return;
                default:
                    throw new Exception("Attempt to modify builder after Building stage");
            }
        }

        void ILazyFactory.Add(ILazy instance)
        {
            AddPostAction(() => { instance.Force(); });
        }

        private IBuilder _parent;
        private BuilderState _state = BuilderState.Building;
        private List<Action> _buildActions = null;
        private List<Action> _postActions = null;
        private List<IBuilder> _children = null;

        public ILazy<T> NewLazy<T>(Func<T> generator)
        {
            var child = new Builder(this);
            var result = this.New(generator);
            child.AddBuildAction(() => { result.Force(); });
            child.DoneBuilding();
            return result;
        }
    }

    /*
    public interface IBuilder<T>
    {
        T Value { get; }
    }

    public class Builder<T> : Builder, IBuilder<T>
    {
        public Builder(
            IBuilder parent)
            : base(parent)
        {
        }

        public Builder(
            IBuilder parent,
            T value )
            : this(parent)
        {
            this.Value = value;
        }

        public T Value
        {
            get
            {
                Force();
                return _value;
            }
            set
            {
                AssertBuildable();
                _value = value;
                DoneBuilding();
            }
        }

        private T _value;
    }
    */

    public enum NewBuilderPhase
    {
        Initial = 0,
        Dependencies,
        Header,
        Body,
        Seal,
        Finalize,
        Final,
    };

    public interface INewBuilder : ILazy
    {
        void AddAction(NewBuilderPhase phase, Action action);
    }

    public abstract class NewBuilder<T> : ILazy<T>, INewBuilder
        where T : class
    {
        public NewBuilder(
            ILazyFactory lazy)
        {
            _lazy = lazy;
            _lazy.Add(this);
        }

        public ILazyFactory LazyFactory { get { return _lazy; } }

        public T Value
        {
            get
            {
                if( _value == null )
                {
                    Force();
                    if( _value == null )
                    {
                        throw new NotImplementedException();
                    }
                }
                return _value;
            }
        }

        protected void SetValue( T value )
        {
            _value = value;
        }

        public void Force()
        {
            FlushActions(NewBuilderPhase.Finalize);
        }

        public void Force(NewBuilderPhase phase)
        {
            FlushActions(phase);
        }


        public void AddAction(Action action)
        {
            AddAction(NewBuilderPhase.Header, action);
        }

        public void AddAction(NewBuilderPhase phase, Action action)
        {
            if( phase <= _addedActionsPhase && (phase <= NewBuilderPhase.Body))
            {
                throw new Exception("Attempt to add action too late!");
            }

            if (phase <= _flushedActionsPhase)
            {
                throw new Exception("Attempt to add action too late!");
            }

            if( _actionForPhase == null )
            {
                _actionForPhase = new Action[(int)NewBuilderPhase.Final];
            }

            var oldAction = _actionForPhase[(int) phase];
            var newAction = action;

            if (oldAction != null)
            {
                newAction = () => { oldAction(); action(); };
            }

            _actionForPhase[(int) phase] = newAction;
        }

        private void FlushActions(NewBuilderPhase phase)
        {
            for (NewBuilderPhase phaseToRun = _flushedActionsPhase; phaseToRun <= phase; _flushedActionsPhase = phaseToRun, phaseToRun = (NewBuilderPhase)(phaseToRun + 1))
            {
                if (phaseToRun == NewBuilderPhase.Final)
                    break;

                if (phaseToRun > _addedActionsPhase)
                {
                    throw new Exception("Attempt to flush builder too early");
                }

                if (_actionForPhase == null)
                    continue;

                var action = _actionForPhase[(int)phaseToRun];
                if (action == null)
                    continue;

                _actionForPhase[(int)phaseToRun] = null;

                _debugRunningActionsPhase = phaseToRun;
                action();
                _debugRunningActionsPhase = NewBuilderPhase.Initial;
            }
        }

        protected void AssertBuildable()
        {
            AssertBuildable(NewBuilderPhase.Header);
        }

        protected void AssertBuildable(NewBuilderPhase phase)
        {
            if (phase <= _flushedActionsPhase)
            {
                throw new Exception("Attempt to modify builder too late!");
            }
        }

        public void DoneBuilding()
        {
            DoneBuilding(NewBuilderPhase.Final);
        }

        public void DoneBuilding(NewBuilderPhase phase)
        {
            if (phase <= _addedActionsPhase)
            {
                throw new Exception("Attempt to finalize builder too late!");
            }

            _addedActionsPhase = phase;
        }

        protected void AddDependency(ILazy dependency)
        {
            AddAction(NewBuilderPhase.Dependencies, () => { dependency.Force(); });
        }

        protected ILazy<U> NewLazy<U>(Func<U> generator)
        {
            return NewLazy<U>(NewBuilderPhase.Final, generator);
        }

        protected ILazy<U> NewLazy<U>(NewBuilderPhase phase, Func<U> generator)
        {
            return _lazy.New<U>(() => { Force(phase); return generator(); });
        }

        private ILazyFactory _lazy;

        private Action[] _actionForPhase;
        private NewBuilderPhase _addedActionsPhase; // phases for which we are done adding actions
        private NewBuilderPhase _flushedActionsPhase; // phases for which we are done running actions

        private NewBuilderPhase _debugRunningActionsPhase;

        private T _value;
    }
}
