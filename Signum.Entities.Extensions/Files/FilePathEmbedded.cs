﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Entities;
using Signum.Utilities;
using System.ComponentModel;
using System.IO;
using System.Web;
using System.Linq.Expressions;

namespace Signum.Entities.Files
{
    [Serializable]
    public class FilePathEmbedded : EmbeddedEntity, IFile, IFilePath
    {
        public static string ForceExtensionIfEmpty = ".dat";

        public FilePathEmbedded() { }

        public FilePathEmbedded(FileTypeSymbol fileType)
        {
            this.FileType = fileType;
        }

        public FilePathEmbedded(FileTypeSymbol fileType, string readFileFrom)
            : this(fileType)
        {
            this.FileName = Path.GetFileName(readFileFrom);
            this.BinaryFile = File.ReadAllBytes(readFileFrom);
        }

        public FilePathEmbedded(FileTypeSymbol fileType, string fileName, byte[] fileData)
            : this(fileType)
        {
            this.FileName = fileName;
            this.BinaryFile = fileData;
        }

        string fileName;
        [StringLengthValidator(AllowNulls = false, Min = 1, Max = 260), FileNameValidator]
        public string FileName
        {
            get { return fileName; }
            set
            {
                var newValue = fileName;
                if (ForceExtensionIfEmpty.HasText() && !Path.GetExtension(value).HasText())
                    value += ForceExtensionIfEmpty;

                Set(ref fileName, value);
            }
        }

        [Ignore]
        byte[] binaryFile;
        public byte[] BinaryFile
        {
            get { return binaryFile; }
            set
            {
                if (Set(ref binaryFile, value) && binaryFile != null)
                    FileLength = binaryFile.Length;
            }
        }

        public int FileLength { get; internal set; }
        
        static Expression<Func<FilePathEmbedded, string>> FileLengthStringExpression =
          @this => ((long)@this.FileLength).ToComputerSize(true);
        [ExpressionField]
        public string FileLengthString
        {
            get { return FileLengthStringExpression.Evaluate(this); }
        }

        [StringLengthValidator(AllowNulls = true, Min = 3, Max = 260, DisabledInModelBinder = true)]
        public string Suffix { get; set; }

        [Ignore]
        public string CalculatedDirectory { get; set; }

        [NotNullable]
        public FileTypeSymbol FileType { get; internal set; }

        [Ignore]
        internal PrefixPair _prefixPair;
        public void SetPrefixPair(PrefixPair prefixPair)
        {
            this._prefixPair = prefixPair;
        }

        public PrefixPair GetPrefixPair()
        {
            if (this._prefixPair != null)
                return this._prefixPair;

            if (CalculatePrefixPair == null)
                throw new InvalidOperationException("OnCalculatePrefixPair not set");

            this._prefixPair = CalculatePrefixPair(this);

            return this._prefixPair;
        }

        public static Func<FilePathEmbedded, PrefixPair> CalculatePrefixPair;

        public string FullPhysicalPath()
        {
            var pp = this.GetPrefixPair();

            return FilePathUtils.SafeCombine(pp.PhysicalPrefix, Suffix);
        }

        public string FullWebPath()
        {
            var pp = this.GetPrefixPair();

            if (string.IsNullOrEmpty(pp.WebPrefix))
                return null;

            string url = pp.WebPrefix + "/" + FilePathUtils.UrlPathEncode(Suffix.Replace("\\", "/"));
            if (url.StartsWith("http"))
                return url;

            return VirtualPathUtility.ToAbsolute(url);
        }

        public override string ToString()
        {
            return "{0} - {1}".FormatWith(FileName, ((long)FileLength).ToComputerSize(true));
        }

        public static Action<FilePathEmbedded> OnPreSaving;
        protected override void PreSaving(PreSavingContext ctx)
        {
            if (OnPreSaving == null)
                throw new InvalidOperationException("OnPreSaving not set");

            OnPreSaving(this);
        }

      
        protected override void PostRetrieving()
        {
            if (CalculatePrefixPair == null)
                throw new InvalidOperationException("OnCalculatePrefixPair not set");

            this.GetPrefixPair();
        }

        public static Func<FilePathEmbedded, FilePathEmbedded> CloneFunc;
        internal FilePathEmbedded Clone()
        {
            return CloneFunc(this);
        }
    }
}
