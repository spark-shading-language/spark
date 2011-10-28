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
    public abstract class ResMemberDecl : Builder, IResMemberDecl
    {
        public ResMemberDecl(
            IResMemberLineDecl line,
            IBuilder parent,
            SourceRange range,
            Identifier name)
            : base(parent)
        {
            _line = line;
            _range = range;
            _name = name;
        }

        public override string ToString()
        {
            return string.Format("{0} // {1}", _name, _range);
        }

        public IResMemberLineDecl Line { get { return _line; } }

        public SourceRange Range { get { return _range; } }
        public ResLexicalID OriginalLexicalID { get { return Line.OriginalLexicalID; } }
        public ResLexicalID InstanceLexicalID { get { return _instanceLexicalID; } }
        public Identifier Name { get { return _name; } }
        public abstract IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm);

        public static ResMemberDecl CreateInheritedDecl(
            ResolveContext resContext,
            IResContainerBuilderRef resContainer,
            IResMemberLineDecl resLine,
            IBuilder parent,
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
                parent,
                range,
                memberRef);
        }

        public abstract ResMemberDecl CreateInheritedDeclImpl(
            ResolveContext resContext,
            IResContainerBuilderRef resContainer,
            IResMemberLineDecl resLine,
            IBuilder parent,
            SourceRange range,
            IResMemberRef memberRef );

        private IResMemberLineDecl _line;
        private SourceRange _range;
        private ResLexicalID _instanceLexicalID = new ResLexicalID();
        private Identifier _name;
    }

    public class ResMemberNameGroup : Builder, IResMemberNameGroup
    {
        public ResMemberNameGroup(
            IResContainerFacetBuilder parent,
            Identifier name)
            : base(parent)
        {
            if( parent != null )
                parent.AddBuildAction(this.DoneBuilding);

            _container = parent;
            _name = name;
        }

        public Identifier Name { get { return _name; } }

        public IEnumerable<IResMemberCategoryGroup> Categories
        {
            get { Force(); return _categoryGroups.Values; }
        }

        public IEnumerable<ResMemberCategoryGroup> Categories_Build
        {
            get { return _categoryGroups.Values; }
        }

        public ResMemberCategoryGroup GetMemberCategoryGroup(ResMemberCategory category)
        {
            return _categoryGroups.Cache(category.Flavor,
                () => new ResMemberCategoryGroup(_container, this, category));
        }

        public ResMemberCategoryGroup FindCategoryGroup(ResMemberCategory category)
        {
            ResMemberCategoryGroup result = null;
            _categoryGroups.TryGetValue(category.Flavor, out result);
            return result;
        }

        private IResContainerFacetBuilder _container;
        private Identifier _name;

        private Dictionary<ResMemberFlavor, ResMemberCategoryGroup> _categoryGroups = new Dictionary<ResMemberFlavor, ResMemberCategoryGroup>();
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

    public class ResMemberCategoryGroup : Builder, IResMemberCategoryGroup
    {
        public ResMemberCategoryGroup(
            IResContainerFacetBuilder container,
            ResMemberNameGroup nameGroup,
            ResMemberCategory category)
            : base(nameGroup)
        {
            nameGroup.AddBuildAction(this.DoneBuilding);

            _container = container;
            _nameGroup = nameGroup;
            _category = category;
        }

        public ResMemberNameGroup NameGroup { get { return _nameGroup; } }
        public Identifier Name { get { return _nameGroup.Name; } }
        public ResMemberFlavor Flavor { get { return _category.Flavor; } }
        public ResMemberCategory Category { get { return _category; } }

        public IResContainerFacetBuilder Container { get { return _container; } }

        public void AddLine(ResMemberLineDeclBuilder line)
        {
            AssertBuildable();
            _lines.Add(line);
        }

        public IEnumerable<IResMemberLineDecl> Lines
        {
            get
            {
                Force();
                foreach (var lazyLine in _lines)
                    yield return lazyLine.Value;
            }
        }

        public IEnumerable<ResMemberLineDeclBuilder> Lines_Build
        {
            get { return _lines; }
        }

        private IResContainerFacetBuilder _container;
        private ResMemberNameGroup _nameGroup;
        private ResMemberCategory _category;

        private List<ResMemberLineDeclBuilder> _lines = new List<ResMemberLineDeclBuilder>();
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
            ILazyFactory lazy,
            Identifier name,
            ResLexicalID originalLexicalID,
            ResMemberCategory category )
            : base(lazy)
        {
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
