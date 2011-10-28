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
    public struct SourcePos
    {
        public SourcePos(
            int lineNumber,
            int columnNumber,
            int location )
        {
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
            this.Location = location;
        }

        public SourcePos(
            int lineNumber,
            int columnNumber )
            : this(lineNumber, columnNumber, -1)
        {
        }

        public int lineNumber;
        public int columnNumber;
        public int Location;
    }

    public struct SourceRange : QUT.Gppg.IMerge<SourceRange>
    {
        public SourceRange(
            string fileName,
            SourcePos start,
            SourcePos end)
        {
            this.fileName = fileName;
            this.start = start;
            this.end = end;
        }

        public SourceRange(
            string fileName,
            SourcePos pos)
            : this(fileName, pos, pos)
        {
        }

        public SourceRange(
            string fileName)
            : this(fileName, new SourcePos())
        {
        }

        public override string ToString()
        {
            // If start and end are on different lines, then use
            // the start position only...
            if (start.lineNumber != end.lineNumber)
            {
                return string.Format("{0}({1},{2})",
                    fileName,
                    start.lineNumber,
                    start.columnNumber);
            }
            else
            {
                return string.Format("{0}({1},{2}-{3})",
                    fileName,
                    start.lineNumber,
                    start.columnNumber,
                    end.columnNumber);
            }
        }

        public static SourceRange Parse(string str)
        {
            int rangeStartIdx = str.LastIndexOf('(');

            var fileName = str.Substring(0, rangeStartIdx);

            str = str.Substring(rangeStartIdx + 1);
            str = str.Substring(0, str.LastIndexOf(')'));

            int commaIdx = str.IndexOf(',');

            var lineStr = str.Substring(0, commaIdx);
            var colStr = str.Substring(commaIdx + 1);

            var line = int.Parse(lineStr);

            var dashIdx = colStr.IndexOf('-');
            int startCol = 0;
            int endCol = 0;
            if (dashIdx >= 0)
            {
                var startColStr = colStr.Substring(0, dashIdx);
                var endColStr = colStr.Substring(dashIdx + 1);

                startCol = int.Parse(startColStr);
                endCol = int.Parse(endColStr);
            }
            else
            {
                startCol = endCol = int.Parse(colStr);
            }

            return new SourceRange(
                fileName,
                new SourcePos(line, startCol, -1),
                new SourcePos(line, endCol, -1));
        }

        public int Length
        {
            get { return end.Location - start.Location; }
        }

        public string fileName;
        public SourcePos start;
        public SourcePos end;

        public SourceRange Merge(SourceRange last)
        {
            return new SourceRange(
                fileName,
                this.start,
                last.end);
        }
    }
}
