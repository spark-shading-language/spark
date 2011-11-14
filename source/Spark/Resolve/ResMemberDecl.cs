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

using Spark.ResolvedSyntax;

namespace Spark.Resolve
{
    public abstract class ResMemberDecl : IResMemberDecl
    {
        private ILazy<IResMemberLineDecl> _line;
        private SourceRange _range;
        private ResLexicalID _instanceLexicalID = new ResLexicalID();
        private Identifier _name;

        public ResMemberDecl(
            ILazy<IResMemberLineDecl> line,
            SourceRange range,
            Identifier name)
        {
            _line = line;
            _range = range;
            _name = name;
        }

        public override string ToString()
        {
            return string.Format("{0} // {1}", _name, _range);
        }

        // IResMemberDecl

        public SourceRange Range { get { return _range; } }
        public Identifier Name { get { return _name; } }
        public IResMemberLineDecl Line { get { return _line.Value; } }
        public abstract IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm);

        /*
         * 
         * TIM: the following stuff isn't accessible through IResMemberDecl, so why does it exist?
         * 
        public ResLexicalID OriginalLexicalID { get { return Line.OriginalLexicalID; } }
        public ResLexicalID InstanceLexicalID { get { return _instanceLexicalID; } }
         * That includes the following, too:
        */

        public static IResMemberDecl CreateInheritedDecl(
            ResolveContext resContext,
            IResContainerBuilderRef resContainer,
            ILazy<IResMemberLineDecl> resLine,
            SourceRange range,
            IResMemberRef memberRef)
        {
            var originalDecl = memberRef.Decl as ResMemberDecl;
            if (originalDecl == null)
                throw new Exception("Unexpected class of member decl");

            return originalDecl.CreateInheritedDeclImpl(
                resContext,
                resContainer,
                resLine,
                range,
                memberRef);
        }

        public abstract IResMemberDecl CreateInheritedDeclImpl(
            ResolveContext resContext,
            IResContainerBuilderRef resContainer,
            ILazy<IResMemberLineDecl> resLine,
            SourceRange range,
            IResMemberRef memberRef );
    }

    public class ResMemberNameGroupBuilder : NewBuilder<IResMemberNameGroup>
    {
        private IResContainerFacetBuilder _facet;
        private Identifier _name;
        private Dictionary<ResMemberFlavor, ResMemberCategoryGroupBuilder> _categoryGroups =
            new Dictionary<ResMemberFlavor, ResMemberCategoryGroupBuilder>();

        public ResMemberNameGroupBuilder(
            ILazyFactory lazyFactory,
            IResContainerFacetBuilder facet,
            Identifier name )
            : base(lazyFactory)
        {
            if (facet != null)
            {
                facet.AddAction(NewBuilderPhase.Seal, () => DoneBuilding());
                AddDependency(facet);
            }
            DoneBuilding(NewBuilderPhase.Dependencies);

            _facet = facet;
            _name = name;

            var resMemberNameGroup = new ResMemberNameGroup(
                name,
                NewLazy(() => (from cgb in _categoryGroups.Values select cgb.Value).Eager()));
            SetValue(resMemberNameGroup);
        }

        public Identifier Name { get { return _name; } }

        public IEnumerable<ResMemberCategoryGroupBuilder> Categories
        {
            get { return _categoryGroups.Values; }
        }

        public ResMemberCategoryGroupBuilder GetMemberCategoryGroup(ResMemberCategory category)
        {
            ResMemberCategoryGroupBuilder result = null;
            if (_categoryGroups.TryGetValue(category.Flavor, out result))
                return result;

            AssertBuildable();
            result = new ResMemberCategoryGroupBuilder(_facet, this, category);
            _categoryGroups.Add( category.Flavor, result );
            return result;
        }

        public ResMemberCategoryGroupBuilder FindMemberCategoryGroup(ResMemberCategory category)
        {
            ResMemberCategoryGroupBuilder result = null;
            _categoryGroups.TryGetValue(category.Flavor, out result);
            return result;
        }
    }

    public class ResMemberNameGroup : IResMemberNameGroup
    {
        private Identifier _name;
        private ILazy<IEnumerable<IResMemberCategoryGroup>> _categories;

        private Dictionary<ResMemberFlavor, IResMemberCategoryGroup> _cachedCategoryGroups;

        public ResMemberNameGroup(
            Identifier name,
            ILazy<IEnumerable<IResMemberCategoryGroup>> categories )
        {
            _name = name;
            _categories = categories;
        }

        public Identifier Name { get { return _name; } }

        public IEnumerable<IResMemberCategoryGroup> Categories
        {
            get { return _categories.Value; }
        }

        private Dictionary<ResMemberFlavor, IResMemberCategoryGroup> CachedCategoryGroups
        {
            get
            {
                if (_cachedCategoryGroups == null)
                {
                    _cachedCategoryGroups = new Dictionary<ResMemberFlavor, IResMemberCategoryGroup>();
                    foreach (var mcg in _categories.Value)
                        _cachedCategoryGroups.Add(mcg.Flavor, mcg);
                }
                return _cachedCategoryGroups;
            }
        }

        public IResMemberCategoryGroup FindCategoryGroup(ResMemberCategory category)
        {
            return CachedCategoryGroups.Cache(category.Flavor, () => null);
        }
    }

    public class ResMemberNameGroupSpec : IResMemberNameGroupSpec
    {
        public ResMemberNameGroupSpec(
            SourceRange range,
            IResContainerRef containerRef,
            IResMemberNameGroup decl)
        {
            _range = range;
            _containerRef = containerRef;
            _decl = decl;
        }

        public IEnumerable<IResMemberCategoryGroupSpec> Categories
        {
            get
            {
                foreach (var c in _decl.Categories)
                    yield return new ResMemberCategoryGroupSpec(_range, _containerRef, c);
            }
        }

        private SourceRange _range;
        private IResContainerRef _containerRef;
        private IResMemberNameGroup _decl;
    }

    public class ResMemberCategoryGroupBuilder : NewBuilder<IResMemberCategoryGroup>
    {
        private IResContainerFacetBuilder _facetBuilder;
        private ResMemberNameGroupBuilder _nameGroupBuilder;
        private ResMemberCategory _category;

        private List<ResMemberLineDeclBuilder> _lines = new List<ResMemberLineDeclBuilder>();


        public ResMemberCategoryGroupBuilder(
            IResContainerFacetBuilder facetBuilder,
            ResMemberNameGroupBuilder nameGroupBuilder,
            ResMemberCategory category )
            : base(nameGroupBuilder.LazyFactory)
        {
            nameGroupBuilder.AddAction(NewBuilderPhase.Seal, () => DoneBuilding());
            AddDependency(nameGroupBuilder);
            DoneBuilding(NewBuilderPhase.Dependencies);

            _facetBuilder = facetBuilder;
            _nameGroupBuilder = nameGroupBuilder;
            _category = category;

            var resMemberCategoryGroup = new ResMemberCategoryGroup(
                Name,
                Flavor,
                NewLazy(() => (from lineBuilder in _lines select lineBuilder.Value).Eager()));
            SetValue(resMemberCategoryGroup);
        }

        public ResMemberNameGroupBuilder NameGroup { get { return _nameGroupBuilder; } }
        public Identifier Name { get { return _nameGroupBuilder.Name; } }
        public ResMemberFlavor Flavor { get { return _category.Flavor; } }
        public ResMemberCategory Category { get { return _category; } }

        public IResContainerFacetBuilder Container { get { return _facetBuilder; } }

        public void AddLine(ResMemberLineDeclBuilder line)
        {
            AssertBuildable();
            _lines.Add(line);
        }

        public IEnumerable<ResMemberLineDeclBuilder> Lines
        {
            get { return _lines; }
        }

    }

    public class ResMemberCategoryGroup : IResMemberCategoryGroup
    {
        private Identifier _name;
        private ResMemberFlavor _flavor;
        private ILazy<IEnumerable<IResMemberLineDecl>> _lines;

        public ResMemberCategoryGroup(
            Identifier name,
            ResMemberFlavor flavor,
            ILazy<IEnumerable<IResMemberLineDecl>> lines )
        {
            _name = name;
            _flavor = flavor;
            _lines = lines;
        }

        public Identifier Name { get { return _name; } }
        public ResMemberFlavor Flavor { get { return _flavor; } }
        public IEnumerable<IResMemberLineDecl> Lines { get { return _lines.Value; } }
    }

    public class ResMemberCategoryGroupSpec : IResMemberCategoryGroupSpec
    {
        public ResMemberCategoryGroupSpec(
            SourceRange range,
            IResContainerRef containerRef,
            IResMemberCategoryGroup decl)
        {
            _range = range;
            _containerRef = containerRef;
            _decl = decl;
        }

        public SourceRange Range { get { return _range; } }
        public IResContainerRef ContainerRef { get { return _containerRef; } }
        public IResMemberCategoryGroup Decl { get { return _decl; } }

        public IResMemberCategoryGroupRef Bind(SourceRange range, IResExp obj)
        {
            return new ResMemberCategoryGroupRef(range, obj, this);
        }

        private SourceRange _range;
        private IResContainerRef _containerRef;
        private IResMemberCategoryGroup _decl;
    }

    public class ResMemberCategoryGroupRef : IResMemberCategoryGroupRef
    {
        public ResMemberCategoryGroupRef(
            SourceRange range,
            IResExp obj,
            ResMemberCategoryGroupSpec spec)
        {
            _range = range;
            _obj = obj;
            _spec = spec;
        }

        public IResMemberCategoryGroupSpec Spec
        {
            get { return _spec; }
        }

        public ResMemberFlavor Flavor
        {
            get { return _spec.Decl.Flavor; }
        }

        public SourceRange Range
        {
            get { return _range; }
        }

        public IResClassifier Classifier
        {
            get { throw new NotImplementedException(); }
        }

        public IEnumerable<IResMemberRef> Members
        {
            get
            {
                foreach (var c in _spec.Decl.Lines)
                {
                    var memberSpec = new ResMemberSpec(
                        _range,
                        _spec.ContainerRef,
                        c.EffectiveDecl);
                    yield return memberSpec.Bind(_range, _obj);
                }
            }

        }

        private SourceRange _range;
        private IResExp _obj;
        private ResMemberCategoryGroupSpec _spec;
    }

    public class ResMemberLineDecl : IResMemberLineDecl
    {
        private Identifier                          _name;
        private ResLexicalID                        _originalLexicalID;
        private ResMemberCategory                   _category;
        private ILazy<ResMemberConcretenessMode>    _concretenessMode;
        private ILazy<ResMemberDeclMode>            _declMode;
        private ILazy<ResTag[]>                     _tags;
        private ILazy<IResMemberDecl>               _directDecl;
        private ILazy<IResMemberDecl[]>             _inheritedDecls;

        public ResMemberLineDecl(
            Identifier                          name,
            ResLexicalID                        originalLexicalID,
            ResMemberCategory                   category,
            ILazy<ResMemberConcretenessMode>    concretenessMode,
            ILazy<ResMemberDeclMode>            declMode,
            ILazy<ResTag[]>                     tags,
            ILazy<IResMemberDecl>               directDecl,
            ILazy<IResMemberDecl[]>             inheritedDecls )
        {
            _name               = name;
            _originalLexicalID  = originalLexicalID;
            _category           = category;
            _concretenessMode   = concretenessMode;
            _declMode           = declMode;
            _tags               = tags;
            _directDecl         = directDecl;
            _inheritedDecls     = inheritedDecls;
        }

        Identifier  IResMemberLineDecl.Name
        {
            get { return _name; }
        }

        ResLexicalID  IResMemberLineDecl.OriginalLexicalID
        {
            get {  return _originalLexicalID; }
        }
    
        ResMemberCategory  IResMemberLineDecl.Category
        {
            get { return _category; }
        }
    
        IResMemberDecl  IResMemberLineDecl.EffectiveDecl
        {
            get
            {
                if( _directDecl.Value != null )
                    return _directDecl.Value;
                if( _inheritedDecls.Value.Length != 0 )
                    return _inheritedDecls.Value[0];
                return null;
            }
        }
    
        IEnumerable<IResMemberDecl>  IResMemberLineDecl.InheritedDecls
        {
            get { return _inheritedDecls.Value; }
        }
    
        ResMemberConcretenessMode  IResMemberLineDecl.ConcretenessMode
        {
            get { return _concretenessMode.Value; }
        }
    
        ResMemberDeclMode  IResMemberLineDecl.MemberDeclMode
        {
            get { return _declMode.Value; }
        }
    
        IEnumerable<ResTag>  IResMemberLineDecl.Tags
        {
            get { return _tags.Value; }
        }
    }

    public class ResMemberLineDeclBuilder : NewBuilder<IResMemberLineDecl>
    {
        private ResMemberConcretenessMode   _concretenessMode = ResMemberConcretenessMode.Final;
        private ResMemberDeclMode           _declMode = ResMemberDeclMode.Direct;
        private List<ResTag>                _tags = new List<ResTag>();
        private IResMemberDecl              _directDecl;
        private List<IResMemberDecl>        _inheritedDecls = new List<IResMemberDecl>();

        public ResMemberLineDeclBuilder(
            ResMemberCategoryGroupBuilder parent,
            ILazyFactory lazy,
            Identifier name,
            ResLexicalID originalLexicalID,
            ResMemberCategory category )
            : base(lazy)
        {
            parent.AddAction(NewBuilderPhase.Seal, () => DoneBuilding());
            AddDependency(parent);
            DoneBuilding(NewBuilderPhase.Dependencies);

            var resMemberLineDecl = new ResMemberLineDecl(
                name,
                originalLexicalID,
                category,
                NewLazy(() => _concretenessMode),
                NewLazy(() => _declMode),
                NewLazy(() => _tags.ToArray()),
                NewLazy(() => _directDecl),
                NewLazy(() => _inheritedDecls.ToArray()) );
            SetValue(resMemberLineDecl);
        }

        public ResMemberConcretenessMode ConcretenessMode
        {
            get { return _concretenessMode; }
            set { AssertBuildable(); _concretenessMode = value; }
        }

        public ResMemberDeclMode MemberDeclMode
        {
            get { return _declMode; }
            set { AssertBuildable(); _declMode = value; }
        }

        public void AddTag(ResTag tag)
        {
            _tags.Add(tag);
        }

        public void AddTags(IEnumerable<ResTag> tags)
        {
            _tags.AddRange(tags);
        }

        public IResMemberDecl DirectDecl
        {
            get { return _directDecl; }
            set
            {
                AssertBuildable();
                _directDecl = value;
                if (_declMode == ResMemberDeclMode.Inherited)
                    _declMode = ResMemberDeclMode.Extended;
            }
        }

        public IEnumerable<IResMemberDecl> InheritedDecls
        {
            get { return _inheritedDecls; }
            set { AssertBuildable(); _inheritedDecls = value.ToList(); }
        }

        public IResMemberDecl EffectiveDecl
        {
            get
            {
                if (_directDecl != null)
                    return _directDecl;
                if (_inheritedDecls != null && _inheritedDecls.Count != 0)
                    return _inheritedDecls[0];
                return null;
            }
        }
    }



    public class ResStructCategory : ResMemberCategory
    {
        public override ResMemberFlavor Flavor
        {
            get { return ResMemberFlavor.Struct; }
        }
    }

    public class ResAttributeCategory : ResMemberCategory
    {
        public override ResMemberFlavor Flavor
        {
            get { return ResMemberFlavor.Attribute; }
        }
    }

    public class ResFieldCategory : ResMemberCategory
    {
        public override ResMemberFlavor Flavor
        {
            get { return ResMemberFlavor.Field; }
        }
    }

    public class ResMethodCategory : ResMemberCategory
    {
        public override ResMemberFlavor Flavor
        {
            get { return ResMemberFlavor.Method; }
        }
    }

    public class ResElementCategory : ResMemberCategory
    {
        public override ResMemberFlavor Flavor
        {
            get { return ResMemberFlavor.Element; }
        }
    }

    public class ResTypeSlotCategory : ResMemberCategory
    {
        public override ResMemberFlavor Flavor
        {
            get { return ResMemberFlavor.TypeSlot; }
        }
    }

    public class ResConceptClassCategory : ResMemberCategory
    {
        public override ResMemberFlavor Flavor
        {
            get { return ResMemberFlavor.ConceptClass; }
        }
    }
}
