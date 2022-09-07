using Bonobo.PluginSystem;
using Bonobo.PluginSystem.Custom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using System.Windows.Media.Imaging;

namespace OpenBlamPlugin
{
    class GitStageFile : FileAction
    {
        private readonly GitSourceControl sourceControl;
        public GitStageFile(IPluginHost pluginHost, IEnumerable<FileActionParameters> files, GitSourceControl control) :
            base(pluginHost, from f in files select f.FileName)
        {
            sourceControl = control;
        }

        public override string DisplayName => "Stage file";

        public override bool IsEnabled => base.IsEnabled && sourceControl.RepoExists;

        public override void Invoke()
        {
            sourceControl.AddNewFiles(FilePaths);
        }

		public override BitmapImage IconImage
		{
			get
			{
				return Bungie.SourceDepot.SDIcons.GetSDIcon(Bungie.SourceDepot.SDActionIcons.GetLatest);
			}
		}
	}
}
