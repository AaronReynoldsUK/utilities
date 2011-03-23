/*
Scott Willeke - 2004
http://scott.willeke.com 
Portions of the WiX project (http://sourceforge.net/projects/wix) were used when creating this code.
Consider this code licensed under Common Public License Version 1.0 (http://www.opensource.org/licenses/cpl1.0.txt).
*/
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Tools.WindowsInstallerXml.Msi;
using Microsoft.Tools.WindowsInstallerXml.Cab;
using System.Runtime.CompilerServices;

namespace Willeke.Scott.LessMSIerables
{
	public class Wixtracts
	{
		/// <summary>
		/// Provides progress information during an extraction operatation.
		/// </summary>
		public class ExtractionProgress : IAsyncResult
		{
			private string _currentFileName;
			private ExtractionActivity _activity;
			private ManualResetEvent _waitSignal;
			private AsyncCallback _callback;
			private int _totalFileCount;
			private int _filesExtracted;

			public ExtractionProgress(AsyncCallback progressCallback, int totalFileCount)
			{
				_activity = ExtractionActivity.Initializing;
				_currentFileName = "";
				_callback = progressCallback;
				_waitSignal = new ManualResetEvent(false);
				_totalFileCount = totalFileCount;
				_filesExtracted = 0;
			}

			internal void ReportProgress(ExtractionActivity activity, string currentFileName, int filesExtractedSoFar)
			{
				lock (this)
				{
					_activity = activity;
					_currentFileName = currentFileName;
					_filesExtracted = filesExtractedSoFar;
				
					if (this.IsCompleted)
						_waitSignal.Set();

					if (_callback!= null)
						_callback(this);
				}
			}

			/// <summary>
			/// The total number of files to be extracted for this operation.
			/// </summary>
			public int TotalFileCount
			{
				get
				{
					lock (this)
					{
						return _totalFileCount;
					}
				}
			}

			/// <summary>
			/// The number of files extracted so far
			/// </summary>
			public int FilesExtractedSoFar
			{
				get
				{
					lock (this)
					{
						return _filesExtracted;
					}
				}
			}


			/// <summary>
			/// If <see cref="P:Activity"/> is <see cref="ExtractionActivity.ExtractingFile"/>, specifies the name of the file being extracted.
			/// </summary>
			public string CurrentFileName
			{
				get
				{
					lock(this)
					{
						return _currentFileName;
					}
				}
			}

			/// <summary>
			/// Specifies the current activity.
			/// </summary>
			public ExtractionActivity Activity
			{
				get
				{
					lock(this)
					{
						return _activity;
					}
				}
			}
			



			#region IAsyncResult Members

			object IAsyncResult.AsyncState
			{
				get 
				{
					lock (this)
					{
						return this;
					}
				}
			}

			bool IAsyncResult.CompletedSynchronously
			{
				get
				{
					lock (this)
					{
						return false;
					}
				}
			}

			public System.Threading.WaitHandle AsyncWaitHandle
			{
				get
				{
					lock(this)
					{
						return _waitSignal;
					}
				}
			}

			public bool IsCompleted
			{
				get
				{
					lock (this)
					{
						return this.Activity == ExtractionActivity.Complete;
					}
				}
			}
			#endregion
		}
		

		/// <summary>
		/// Specifies the differernt available activities.
		/// </summary>
		public enum ExtractionActivity
		{
			Initializing,
			Uncompressing,
			ExtractingFile, 
			Complete
		}

		public static void ExtractFiles(FileInfo msi, DirectoryInfo outputDir)
		{
			ExtractFiles(msi, outputDir, null, null);
		}

		/// <summary>
		/// Extracts the compressed files from the specified MSI file to the specified output directory.
		/// If specified, the list of <paramref name="filesToExtract"/> objects are the only files extracted.
		/// </summary>
		/// <param name="filesToExtract">The files to extract or null or empty to extract all files.</param>
		/// <param name="progressCallback">Will be called during during the operation with progress information, and upon completion. The argument will be of type <see cref="T:ExtractionProgress"/>.</param>
		public static void ExtractFiles(FileInfo msi, DirectoryInfo outputDir, MsiFile[] filesToExtract, AsyncCallback progressCallback)
		{
			if (msi == null)
				throw new ArgumentNullException("msi");
			if (outputDir == null)
				throw new ArgumentNullException("outputDir");
			
			ExtractionProgress progress = new ExtractionProgress(progressCallback, filesToExtract.Length);

			int filesExtractedSoFar = 0;

			if (!msi.Exists)
			{
				Trace.WriteLine("File \'" + msi.FullName + "\' not found.");
				progress.ReportProgress(ExtractionActivity.Complete, "", filesExtractedSoFar);
				return;
			}

			
			progress.ReportProgress(ExtractionActivity.Initializing, "", filesExtractedSoFar);
			outputDir.Create();
			
			Database msidb = new Database(msi.FullName, OpenDatabase.ReadOnly);
			try
			{
				if (filesToExtract == null || filesToExtract.Length < 1)
					filesToExtract = MsiFile.CreateMsiFilesFromMSI(msidb);
				//map short file names to the msi file entry
				//Dictionary<string, MsiFile> fileEntryMap = new Dictionary<string, MsiFile>(StringComparer.InvariantCultureIgnoreCase);
				Hashtable fileEntryMap = new Hashtable(CaseInsensitiveHashCodeProvider.DefaultInvariant, CaseInsensitiveComparer.DefaultInvariant);
				foreach (MsiFile fileEntry in filesToExtract)
					fileEntryMap[fileEntry.File] = fileEntry;

				//extract ALL the files to the folder:
				progress.ReportProgress(ExtractionActivity.Uncompressing, "", filesExtractedSoFar);
				DirectoryInfo[] mediaCabs = ExplodeAllMediaCabs(msidb, outputDir);

				// now rename or remove an files not desired.
				foreach (DirectoryInfo cabDir in mediaCabs)
				{
					foreach (FileInfo sourceFile in cabDir.GetFiles())
					{						
						MsiFile entry = fileEntryMap[sourceFile.Name] as MsiFile;

						if (entry != null)
						{
							progress.ReportProgress(ExtractionActivity.ExtractingFile, entry.LongFileName, filesExtractedSoFar);
							
							string destName = string.Concat(outputDir.FullName, Path.DirectorySeparatorChar, entry.LongFileName);
							if (File.Exists(destName))
							{
								//make unique
								Trace.WriteLine(string.Concat("Duplicate file found \'", destName, "\'"));
								int duplicateCount = 0;
								string uniqueName = destName;
								do
								{
									uniqueName = string.Concat(destName, ".", "duplicate", ++duplicateCount);
								} while (File.Exists(uniqueName)); ;
								destName = uniqueName;
							}
							//rename
							Trace.WriteLine(string.Concat("Renaming File \'", sourceFile.Name, "\' to \'", entry.LongFileName, "\'"));
							sourceFile.MoveTo(destName);
						}
						filesExtractedSoFar++;
					}
					cabDir.Delete(true);
					
				}
			}
			finally
			{
				msidb.Close();
				progress.ReportProgress(ExtractionActivity.Complete, "", filesExtractedSoFar);
			}
		}

		
		/// <summary>
		/// Dumps the entire contents of each cab into it's own subfolder in the specified baseOutputPath.
		/// </summary>
		/// <remarks>
		/// A list of Directories containing the files that were the contents of the cab files.
		/// </remarks>
		private static DirectoryInfo[] ExplodeAllMediaCabs(Database msidb, DirectoryInfo baseOutputPath)
		{
			ArrayList/*<DirectoryInfo>*/ cabFolders = new ArrayList();

			const string tableName = "Media";
			if (!msidb.TableExists(tableName))
			{
				return (DirectoryInfo[])cabFolders.ToArray(typeof(DirectoryInfo));
			}
			//string query = String.Concat("SELECT * FROM `", tableName, "` WHERE `DiskId` = `", diskIdToExtract, "`");
			string query = String.Concat("SELECT * FROM `", tableName, "`");
			using (Microsoft.Tools.WindowsInstallerXml.Msi.View view = msidb.OpenExecuteView(query))
			{
				Record record;
				while (view.Fetch(out record))
				{
					const int MsiInterop_Media_Cabinet = 4;
					string cabSourceName = record[MsiInterop_Media_Cabinet];

					DirectoryInfo cabFolder;
					// ensure it's a unique folder
					int uniqueCounter = 0;
					do
					{
						cabFolder = new DirectoryInfo(Path.Combine(baseOutputPath.FullName, string.Concat("_cab_", cabSourceName, ++uniqueCounter)));
					} while (cabFolder.Exists);

					Trace.WriteLine(string.Concat("Exploding media cab \'", cabSourceName, "\' to folder \'", cabFolder.FullName, "\'."));
					if (0 < cabSourceName.Length)
					{
						if (cabSourceName.StartsWith("#"))
						{
							cabSourceName = cabSourceName.Substring(1);

							// extract cabinet, then explode all of the files to a temp directory
							string cabFileSpec = Path.Combine(baseOutputPath.FullName, cabSourceName);
							FileInfo mediaCab = new FileInfo(cabFileSpec);

							ExtractCabFromPackage(cabFileSpec, cabSourceName, msidb);
							WixExtractCab extCab = new WixExtractCab();
							if (File.Exists(cabFileSpec))
							{
								cabFolder.Create();
								// track the created folder so we can return it in the list.
								cabFolders.Add(cabFolder);

								extCab.Extract(cabFileSpec, cabFolder.FullName);
							}
							extCab.Close();
							File.Delete(cabFileSpec);
						}
					}
				}
			}
			return (DirectoryInfo[])cabFolders.ToArray(typeof(DirectoryInfo));
		}


		/// <summary>
		/// Write the Cab to disk.
		/// </summary>
		/// <param name="filePath">Specifies the path to the file to contain the stream.</param>
		/// <param name="cabName">Specifies the name of the file in the stream.</param>
		public static void ExtractCabFromPackage(string filePath, string cabName, Database inputDatabase)
		{
			using (Microsoft.Tools.WindowsInstallerXml.Msi.View view = inputDatabase.OpenExecuteView(String.Concat("SELECT * FROM `_Streams` WHERE `Name` = '", cabName, "'")))
			{
				Record record;
				if (view.Fetch(out record))
				{
					FileStream cabFilestream = null;
					BinaryWriter writer = null;
					try
					{
						cabFilestream = new FileStream(filePath, FileMode.Create);

						// Create the writer for data.
						writer = new BinaryWriter(cabFilestream);
						int count = 512;
						byte[] buf = new byte[count];
						while (count == buf.Length)
						{
							const int MsiInterop_Storages_Data = 2;//From wiX:Index to column name Data into Record for row in Msi Table Storages
							count = record.GetStream((int)MsiInterop_Storages_Data, buf, count);
							if (buf.Length > 0)
							{
								// Write data to Test.data.
								writer.Write(buf);
							}
						}
					}
					finally
					{
						if (writer != null)
						{
							writer.Close();
						}

						if (cabFilestream != null)
						{
							cabFilestream.Close();
						}
					}
				}
			}
		}
	}
}
