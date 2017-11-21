﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Language
{
	class MSBuildDocumentValidator : MSBuildVisitor
	{
		readonly MSBuildResolveContext context;
		readonly string filename;
		readonly ITextDocument document;

		public MSBuildDocumentValidator (MSBuildResolveContext context, string filename, ITextDocument document)
		{
			this.context = context;
			this.filename = filename;
			this.document = document;
		}

		protected void AddError (ErrorType errorType, string message, DocumentRegion region)
			=> context.Errors.Add (new Error (errorType, message, region));

		protected void AddError (string message, DocumentRegion region) => AddError (ErrorType.Error, message, region);

		protected void AddWarning (string message, DocumentRegion region) => AddError (ErrorType.Warning, message, region);

		protected override void VisitUnknownElement (XElement element)
		{
			AddError ($"Unknown element '{element.Name.FullName}'", element.Region);
			base.VisitUnknownElement (element);
		}

		protected override void VisitUnknownAttribute (XElement element, XAttribute attribute)
		{
			AddError ($"Unknown attribute '{attribute.Name.FullName}'", attribute.Region);
			base.VisitUnknownAttribute (element, attribute);
		}

		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			base.VisitResolvedElement (element, resolved);

			foreach (var rat in resolved.Attributes) {
				if (rat.Required && !rat.IsAbstract) {
					var xat = element.Attributes.Get (new XName (rat.Name), true);
					if (xat == null) {
						AddError ($"{element.Name.Name} must have attribute {rat.Name}", element.GetNameRegion ());
					}
				}
			}

			switch (resolved.Kind) {
			case MSBuildKind.Project:
				if (!filename.EndsWith (".props", System.StringComparison.OrdinalIgnoreCase)) {
					ValidateProjectHasTarget (element);
				}
				break;
			case MSBuildKind.OnError:
				ValidateOnErrorOnlyFollowedByOnError (element);
				break;
			case MSBuildKind.Otherwise:
				ValidateOtherwiseIsLastElement (element);
				break;
			case MSBuildKind.Output:
				ValidateOutputHasPropertyOrItemName (element);
				break;
			case MSBuildKind.UsingTask:
				ValidateUsingTaskHasAssembly (element);
				break;
			case MSBuildKind.Import:
				ValidateImportOnlyHasVersionIfHasSdk (element);
				break;
			case MSBuildKind.Item:
				ValidateItemAttributes (element);
				break;
			}
		}

		void ValidateProjectHasTarget (XElement element)
		{
			if (element.Attributes.Get (new XName ("Sdk"), true) != null) {
				return;
			}

			foreach (var child in element.Nodes) {
				if (child is XElement projectChild && !projectChild.Name.HasPrefix) {
					switch (projectChild.Name.Name.ToLower ()) {
					case "target":
					case "import":
						return;
					}
				}
			}

			AddWarning ($"Project should have Sdk, Target or Import", element.GetNameRegion ());
		}

		void ValidateOnErrorOnlyFollowedByOnError (XElement element)
		{
			if (!element.NextSiblingElement ().NameEquals ("OnError", true)) {
				AddError (
					$"OnError may only be followed by other OnError elements",
					element.NextSiblingElement ().GetNameRegion ());
			}
		}

		void ValidateOtherwiseIsLastElement (XElement element)
		{
			if (element.NextSiblingElement () != null) {
				AddError (
					$"Otherwise must be the last element in a Choose",
					element.NextSiblingElement ().GetNameRegion ());
			}
		}

		void ValidateOutputHasPropertyOrItemName (XElement element)
		{
			bool foundItemOrPropertyName = false;
			foreach (var att in element.Attributes) {
				if (att.NameEquals ("ItemName", true) || att.NameEquals ("PropertyName", true)) {
					foundItemOrPropertyName = true;
					break;
				}
			}
			if (!foundItemOrPropertyName) {
				AddError (
					$"Output element must have PropertyName or ItemName attribute",
					element.GetNameRegion ());
			}
		}

		void ValidateUsingTaskHasAssembly (XElement element)
		{
			bool foundAssemblyAttribute = false;
			foreach (var att in element.Attributes) {
				if (att.NameEquals ("AssemblyName", true) || att.NameEquals ("AssemblyFile", true)) {
					if (foundAssemblyAttribute) {
						AddError (
							$"UsingTask may have only one AssemblyName or AssemblyFile attribute",
							att.GetNameRegion ());
					}
					foundAssemblyAttribute = true;
				}
			}
			if (!foundAssemblyAttribute) {
				AddError (
					$"UsingTask must have AssemblyName or AssemblyFile attribute",
					element.GetNameRegion ());
			}

			bool foundParameterGroup = false, foundTaskBody = false;
			foreach (var child in element.Elements) {
				if (child.NameEquals ("ParameterGroup", true)) {
					if (foundParameterGroup) {
						AddError (
							$"UsingTask may only have one ParameterGroup",
							child.GetNameRegion ());
					}
					foundParameterGroup = true;
				}
				if (child.NameEquals ("TaskBody", true)) {
					if (foundTaskBody) {
						AddError (
							$"UsingTask may only have one TaskBody",
							child.GetNameRegion ());
					}
					foundTaskBody = true;
				}
			}
			if (foundParameterGroup != foundTaskBody) {
				AddError (
					$"UsingTask must have both TaskBody and ParameterGroup, or neither",
					element.GetNameRegion ());
			}
		}

		void ValidateImportOnlyHasVersionIfHasSdk (XElement element)
		{
			if (element.Attributes.Get (new XName ("Sdk"), true) != null) {
				return;
			}

			foreach (var att in element.Attributes) {
				if (att.NameEquals ("Version", true)) {
					AddError (
						$"Import may only have a Version if it has an Sdk",
						att.GetNameRegion ());
				}
				if (att.NameEquals ("MinVersion", true)) {
					AddError (
						$"Import may only have a MinVersion if it has an Sdk",
						att.GetNameRegion ());
				}
			}
		}

		void ValidateItemAttributes (XElement element)
		{
			bool hasInclude = false, hasUpdate = false, hasRemove = false;
			foreach (var att in element.Attributes) {
				hasInclude |= att.NameEquals ("Include", true);
				hasUpdate |= att.NameEquals ("Update", true);
				hasRemove |= att.NameEquals ("Remove", true);
				if (att.NameEquals ("KeepMetadata", true) || att.NameEquals ("RemoveMetadata", true) || att.NameEquals ("KeepDuplicates", true)) {
					if (!(element.Parent?.Parent is XElement t && t.NameEquals ("Target", true))) {
						AddError (
							$"{att.Name.Name} is only valid within a target",
							att.GetNameRegion ());
					}
				}
			}

			if (!hasInclude && !hasRemove && !hasUpdate) {
				AddError (
					$"Items must have Include, Update or Remove attributes",
					element.GetNameRegion ());
			}
		}

		protected override void VisitResolvedAttribute (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);

			if (string.IsNullOrWhiteSpace (attribute.Value)) {
				if (resolvedAttribute.Required) {
					AddError ($"Required attribute has empty value", attribute.GetNameRegion ());
				} else {
					AddWarning ($"Attribute has empty value", attribute.GetNameRegion ());
				}
				return;
			}

			var info = context.GetSchemas ().GetAttributeInfo (resolvedAttribute, element.Name.Name, attribute.Name.Name);

			if (info.DefaultValue != null && string.Equals (info.DefaultValue, attribute.Value)) {
				AddWarning ($"Attribute has default value", attribute.GetValueRegion (document));
			}

			var kind = MSBuildCompletionExtensions.InferValueKindIfUnknown (info);
			bool allowExpressions = kind.AllowExpressions ();
			bool allowLists = kind.AllowLists () || info.ValueSeparators?.Length > 0;
			kind = kind.GetScalarType ();

			//TODO: comma-separated lists
			var expr = new Expression ();
			expr.Parse (attribute.Value, ExpressionParser.ParseOptions.AllowItemsMetadataAndSplit);

			for (int i = 0; i < expr.Collection.Count; i++) {
				var val = expr.Collection [i];
				if (val is InvalidExpressionError err) {
					var startOffset = attribute.GetValueStartOffset (document) + err.Position;
					this.AddError (
						$"Invalid expression: {err.Message}",
						new DocumentRegion (
							document.OffsetToLocation (startOffset),
							document.OffsetToLocation (startOffset + (attribute.Value.Length - err.Position))
						)
					);
					return;
				}
				if (val is string s) {
					if (s == ";") {
						if (!allowLists) {
							AddValueError ("Attribute does not allow lists");
							return;
						}
						continue;
					}
					//it's a pure value if the items before & ahead of it are list boundaries or ';'
					var isPureLiteralValue =
						(i == 0 || (expr.Collection [i - 1] is string prev && prev == ";")) &&
						(i + 1 == expr.Collection.Count || (expr.Collection [i + 1] is string next && next == ";"));
						 
					if (isPureLiteralValue) {
						ValidateValue (attribute, s, kind, info.Values);
						continue;
					}
				}
				if (!allowExpressions) {
					AddValueError ("Attribute does not allow expressions");
				}

				//items are implicitly lists
				if (val is ItemReference ir) {
					if (!allowLists) {
						AddValueError ("Attribute does not allow lists");
						return;
					}
				}

				//TODO: can we validate properties, metadata, items in any meaningful way?
			}

			void AddValueError (string e) => this.AddError (e, attribute.GetValueRegion (document));
		}

		void ValidateValue (XAttribute attribute, string value, MSBuildValueKind kind, List<ConstantInfo> knownValues)
		{
			if (knownValues != null && knownValues.Count != 0) {
				foreach (var kv in knownValues) {
					if (string.Equals (kv.Name, value, System.StringComparison.OrdinalIgnoreCase)) {
						return;
					}
				}
				AddError ($"Unknown value '{value}'");
				return;
			}
			switch (kind) {
			case MSBuildValueKind.Guid:
				if (!System.Guid.TryParseExact (value, "B", out _)) {
					AddError ("Invalid GUID value");
				}
				break;
			case MSBuildValueKind.Int:
				if (!System.Int64.TryParse (value, out _)) {
					AddError ("Invalid integer value");
				}
				break;
			case MSBuildValueKind.Bool:
				if (!System.Boolean.TryParse (value, out _)) {
					AddError ("Invalid boolean value");
				}
				break;
			case MSBuildValueKind.Url:
				if (!System.Uri.TryCreate (value, System.UriKind.Absolute, out _)) {
					AddError ("Invalid URL");
				}
				break;
			case MSBuildValueKind.Version:
				if (!System.Version.TryParse (value, out _)) {
					AddError ("Invalid version");
				}
				break;
				
			}

			void AddError (string e) => this.AddError (e, attribute.GetValueRegion (document));
		}
	}
}