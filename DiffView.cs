using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace OpenBlamPlugin
{
    internal class DiffViewHelper
    {
        public DiffViewHelper(GitSourceControl parent)
        {
            sourceControl = parent;
        }
        private readonly GitSourceControl sourceControl;

        static private FileStream GetTempFile()
        {
            string fileName = Path.GetTempFileName();
            return File.Create(fileName, 4096, System.IO.FileOptions.DeleteOnClose);
        }
    }
}
