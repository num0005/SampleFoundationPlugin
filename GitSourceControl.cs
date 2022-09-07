using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.IO;
using Bonobo.PluginSystem;
using Bonobo.PluginSystem.Custom;
using Bonobo.PluginSystem.Custom.Windowing;
using System.Windows.Threading;
using Corinth.Connections;
using LibGit2Sharp;
using Bungie.Reactive;

namespace OpenBlamPlugin
{
	[BonoboPlugin("Git support plugin", Priority = InitializationPriority.High)]
	public class GitSourceControl : BonoboPlugin, IFileActionProvider, ISourceControlMenuProvider, ISourceControlProvider, IAsyncSourceControlProvider
    {
        private readonly IPluginHost pluginHost;
        private readonly string repoPath;
        private readonly Repository repository;
        private string userName;

        private static readonly IReadOnlyList<string> emptyStringList = new List<string>();

        public IPluginHost Host => pluginHost;

        // needed because source depot used int32 IDs 
        readonly private GitObjectMapping mapping = new GitObjectMapping();
        public GitSourceControl(IPluginHost pluginHost) : base(pluginHost)
		{
            this.pluginHost = pluginHost;
            repoPath = Repository.Discover("tags");
            if (repoPath != null)
            {
                repository = new Repository(repoPath);
                userName = Environment.UserName;
            }
        }

        private string TranslatePath(string filepath)
        {
            string fullRepoPath = Path.GetFullPath(repoPath.Replace(".git", "")).ToLower();
            string fileFullPath = Path.GetFullPath(filepath).ToLower();
            return fileFullPath.Replace(fullRepoPath, "");
        }

        private Identity GetIdentity()
        {
            return new Identity(userName, $"{userName}@user.halo.example");
        }

        private Signature GetSignatureNow()
        {
            return new Signature(GetIdentity(), DateTime.UtcNow);
        }

        /// <summary>
        /// Map from git file status to Corinth source control states
        /// </summary>
        /// <param name="gitStatus"></param>
        /// <returns></returns>
        private SourceControlFileState mapFileStatus(FileStatus gitStatus)
        {
            switch (gitStatus)
            {
                case FileStatus.Nonexistent:
                case FileStatus.NewInWorkdir:
                    return SourceControlFileState.NotInDepot;
                case FileStatus.Unreadable:
                    return SourceControlFileState.MissingFromClientView;
                case FileStatus.Unaltered:
                    return SourceControlFileState.UpToDate;
                case FileStatus.Conflicted:
                    return SourceControlFileState.WaitingForState; // idk if this is the best code to return
                case FileStatus.DeletedFromIndex:
                    return SourceControlFileState.MarkedForAdd; // idk
                case FileStatus.DeletedFromWorkdir:
                    return SourceControlFileState.MissingFromClientDisk;
                case FileStatus.Ignored:
                    return SourceControlFileState.CheckedOutOnAnotherClient; // not sure about this too
                case FileStatus.ModifiedInIndex:
                case FileStatus.ModifiedInWorkdir:
                    return SourceControlFileState.CheckedOutOnThisClient;
                case FileStatus.NewInIndex:
                    return SourceControlFileState.MarkedForAdd;
                default:
                    return SourceControlFileState.Offline;

            }
        }

        private SourceControlFile GetSourceControlFile(string fileName)
        {
            string path = TranslatePath(fileName);
            FileStatus status = repository.RetrieveStatus(path);
            SourceControlFileState state = mapFileStatus(status);

            return new SourceControlFile(
                fileName,
                state,
                state != SourceControlFileState.UpToDate, // the UI gets mad if an up to date file is writable
                emptyStringList,
                emptyStringList,
                true);
        }

        public bool RepoExists => repository != null;

        public IObservableValue<bool> IsAvailable => throw new NotImplementedException();

        public void AddNewFiles(IEnumerable<string> fileNames)
        {
            lock (repository)
            {
                foreach (string filename in fileNames)
                    repository.Index.Add(TranslatePath(filename));
            }
        }

        public bool CheckIn(IEnumerable<string> fileNames)
        {
            return CheckIn(null, fileNames);
        }

        public bool CheckIn(string changeDescription, IEnumerable<string> fileNames)
        {
            lock (repository)
            {
                // stage the files
                foreach (string filename in fileNames)
                    repository.Index.Add(TranslatePath(filename));
                if (changeDescription is null)
                {
                    // if we only have one file we can improve the commit message
                    if (fileNames.Count() == 1)
                    {
                        changeDescription = $"Updated {TranslatePath(fileNames.First())}";
                    } else
                    {
                        StringBuilder builder = new StringBuilder($"Updated {fileNames.Count()} files \n\n");
                        foreach (string filename in fileNames)
                        {
                            builder.Append("* ");
                            builder.AppendLine(TranslatePath(filename));
                        }
                        changeDescription = builder.ToString();
                    }
                }

                repository.Commit(changeDescription, GetSignatureNow(), GetSignatureNow());
            }

            return true;
        }

        private bool noCheckoutMessageShown = false;

        public bool CheckOut(IEnumerable<string> fileNames, bool sync = true, bool scratch = false, bool demoteToScratchSilently = false)
        {
            Bungie.Utilities.Log.Warn("CheckOut not implemented - git doesn't support op");
            if (!noCheckoutMessageShown)
            {
                noCheckoutMessageShown = true;
                MessageBox.Show("Git does not support checking out individual files!");
            }
            return false;
        }

        public bool CheckOutNoScratch(IEnumerable<string> fileNames, bool sync = true)
        {
            return CheckOut(fileNames, sync, false, false);
        }

        public bool Delete(IEnumerable<string> fileNames, bool scratch = false)
        {
            throw new NotImplementedException();
        }

        public bool DeleteWithoutPrompting(string changeDescription, IEnumerable<string> fileNames, bool scratch = false)
        {
            
            foreach (string file in fileNames)
                File.Delete(file);
            return CheckIn(changeDescription, fileNames);
        }

        public IEnumerable<IFileAction> GetActions(IEnumerable<FileActionParameters> files)
        {
            var actions = new List<IFileAction>();
            actions.Add(new GitStageFile(base.PluginHost, files, this));
            return actions;
        }

        public IEnumerable<string> GetCheckedOutClients(string fileName)
        {
            // no remote clients
            return emptyStringList;
        }

        public IEnumerable<string> GetFilesNotInDefaultChangelist(IEnumerable<string> fileNames)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<SourceControlFile> GetFileStateForDirectory(string directoryPath)
        {
            if (repository == null)
                return new List<SourceControlFile>();
            List<SourceControlFile> files = new List<SourceControlFile>();
            foreach (string filespec in Directory.EnumerateFiles(directoryPath))
            {
                files.Add(GetSourceControlFile(filespec));
            }

            return files;
        }

        public IEnumerable<SourceControlFile> GetFileStates(string fileSpecs)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<SourceControlFile> GetFileStates(IEnumerable<string> fileSpecs)
        {
            List<SourceControlFile> files = new List<SourceControlFile>();
            foreach(string filespec in fileSpecs)
            {
                files.Add(GetSourceControlFile(filespec));
            }

            return files;
        }

        public int GetLastDepotRevision(string fileName)
        {
            // todo(num005): get the latest revision for the file not repo
            lock (repository)
            {
                Branch tracked = repository.Head.TrackedBranch;
                if (tracked != null)
                {
                    return mapping.AddObject(tracked.Tip);
                }
            }
            return 0;
        }

        public void GetLatest(IEnumerable<string> fileNames, bool force)
        {
            Bungie.Utilities.Log.Warn("GetLatest not implemented - git doesn't support op");
        }

        public IEnumerable<SourceControlFile> GetOpenedFiles()
        {
            throw new NotImplementedException();
        }

        public SourceControlFile GetSingleFileState(string fileName)
        {
            return GetSourceControlFile(fileName);
        }

        public IEnumerable<MenuItemDescription> GetSourceControlMenuItems<T>(Func<T, IEnumerable<SourceControlMenuFile>> getFocusedWindowMenuFilesFunc, Func<T, bool> saveFocusedWindowFunc) where T : System.Windows.FrameworkElement
        {
            return new List<MenuItemDescription>();
        }

        public bool IsFileOperationAvailable(SourceControlOperation operation, Corinth.Connections.SourceControlFileState state, bool isWritable, string file = null)
        {
            // filter out ops git can't support
            if (operation != SourceControlOperation.CheckIn
                    //&& operation != SourceControlOperation.ShowHistory
                    && operation != SourceControlOperation.Delete
                    && operation != SourceControlOperation.AddToDepot)
                return false;
            // todo(num005) figure out this part
            return true;
        }

        public void MakeWritable(IEnumerable<string> fileNames)
        {
            // already writable
        }

        public void MakeWritable(IEnumerable<string> fileNames, IEnumerable<string> subDirectoryExtensions)
        {
            // already writable
        }

        public void OnlineWithoutPrompting(string changeDescription, IEnumerable<string> fileNames)
        {
            throw new NotImplementedException();
        }

        public void RefreshAvailability()
        {
            throw new NotImplementedException();
        }

        public bool Rename(IEnumerable<SourceControlRenameFilePair> renameFiles, bool checkIn)
        {
            throw new NotImplementedException();
        }

        public void Resolve(IEnumerable<string> filenames, global::Corinth.Connections.IChangelist.ResolveAcceptAction acceptAction)
        {
            throw new NotImplementedException();
        }

        public void RevertUnchangedFiles(IEnumerable<string> filenames)
        {
            throw new NotImplementedException();
        }

        public void ShowDiff(IEnumerable<string> fileNames)
        {
            throw new NotImplementedException();
        }

        public void ShowHistory(IEnumerable<string> fileNames)
        {
            throw new NotImplementedException();
        }

        public void SyncToChangelist(IEnumerable<string> fileNames, int changelist)
        {
            throw new NotImplementedException();
        }

        public void UndoCheckOut(IEnumerable<string> fileNames)
        {
            throw new NotImplementedException();
        }

        public void UndoCheckOutWithoutPrompting(IEnumerable<string> fileNames)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// For when you don't want to make something async
        /// </summary>
        /// <typeparam name="t"></typeparam>
        internal class FakeObservable<t> : IObservable<t>
        {
            internal class DummyUnsubscribe : IDisposable
            {
                public void Dispose()
                {
                }
            }
            public FakeObservable(t value)
            {
                this.value = value;
            }
            public IDisposable Subscribe(IObserver<t> observer)
            {
                observer.OnNext(value);
                observer.OnCompleted();

                return new DummyUnsubscribe();
            }

            private readonly t value; 
        }

        public IObservable<IEnumerable<SourceControlFile>> GetFileStatesAsync(IEnumerable<string> fileSpecs)
        {
            return new FakeObservable<IEnumerable<SourceControlFile>>(GetFileStates(fileSpecs));
        }

        public IObservable<IEnumerable<SourceControlFile>> GetFileStateForDirectoryAsync(IEnumerable<string> directoryPath)
        {
            // todo(num005) make this async
            List<SourceControlFile> files = new List<SourceControlFile>();
            foreach (string directory in directoryPath)
            {
                files.AddRange(GetFileStateForDirectory(directory));
            }
            return new FakeObservable<IEnumerable<SourceControlFile>>(files);
        }
    }
}
