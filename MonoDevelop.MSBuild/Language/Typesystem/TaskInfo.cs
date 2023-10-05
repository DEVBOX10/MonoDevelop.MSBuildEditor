// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	public class TaskInfo : BaseSymbol, IDeprecatable
	{
		public Dictionary<string, TaskParameterInfo> Parameters { get; }

		// only used for intrinsic tasks
		internal TaskInfo (string name, DisplayText description, bool isIntrinsic, params TaskParameterInfo[] parameters) : this (name, description, null, null, null, null, 0, false, null)
		{
			foreach (var p in parameters) {
				Parameters.Add (p.Name, p);
			}
			IsIntrinsic = isIntrinsic;
		}

		public TaskInfo (string name, DisplayText description, string typeName, string assemblyName, string assemblyFile, string declaredInFile, int declaredAtOffset, bool isDeprecated, string deprecationMessage, Dictionary<string, TaskParameterInfo> parameters = null)
			: base (name, description)
		{
			TypeName = typeName;
			AssemblyName = assemblyName;
			AssemblyFile = assemblyFile;
			DeclaredInFile = declaredInFile;
			DeclaredAtOffset = declaredAtOffset;
			IsDeprecated = isDeprecated || !string.IsNullOrEmpty (deprecationMessage);
			DeprecationMessage = deprecationMessage;
			Parameters = parameters ?? new Dictionary<string, TaskParameterInfo> ();
		}

		public string TypeName { get; }
		public string AssemblyName { get; }
		public string AssemblyFile { get; }

		public string DeclaredInFile { get; }
		public int DeclaredAtOffset  { get; }

		public bool IsInferred => DeclaredInFile == null;

		public bool ForceInferAttributes { get; set; }

		public bool IsIntrinsic { get; }

		public bool IsDeprecated { get; }

		public string DeprecationMessage { get; }
	}

	public class TaskParameterInfo : VariableInfo
	{
		public bool IsOutput { get; }
		public bool IsRequired { get; }

		public TaskParameterInfo (
			string name, DisplayText description, bool isRequired,
			bool isOutput, MSBuildValueKind kind, bool isDeprecated = false, string deprecationMessage = null)
			: base (name, description, kind, null, null, isDeprecated, deprecationMessage)
		{
			IsOutput = isOutput;
			IsRequired = isRequired;
		}
	}
}
