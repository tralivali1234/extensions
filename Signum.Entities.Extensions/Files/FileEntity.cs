﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Entities;
using Signum.Entities.Basics;
using System.IO;
using Signum.Utilities;
using Signum.Services;

namespace Signum.Entities.Files
{
    [Serializable, EntityKind(EntityKind.SharedPart, EntityData.Transactional), TicksColumn(false)]
    public class FileEntity : ImmutableEntity, IFile
    {
        public FileEntity() { }

        public FileEntity(string path)
        {
            this.FileName = Path.GetFileName(path);
            this.BinaryFile = File.ReadAllBytes(path);
        }

        [StringLengthValidator(AllowNulls = false, Min = 3, Max = 254)]
        public string FileName { get; set; }

        public string Hash { get; private set; }

        byte[] binaryFile;
        public byte[] BinaryFile
        {
            get { return binaryFile; }
            set
            {
                if (Set(ref binaryFile, value))
                    Hash = CryptorEngine.CalculateMD5Hash(binaryFile);
            }
        }

        public override string ToString()
        {
            return "{0} {1}".FormatWith(FileName, BinaryFile?.Let(bf => StringExtensions.ToComputerSize(bf.Length)) ?? "??");
        }

        public string FullWebPath()
        {
            return null; 
        }
    }
}
