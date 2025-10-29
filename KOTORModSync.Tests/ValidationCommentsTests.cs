// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class ValidationCommentsTests
	{
		[Test]
		public void ComponentValidationContext_AddsComponentIssue()
		{
			// Arrange
			var context = new ComponentValidationContext();
			var guid = Guid.NewGuid();

			// Act
			context.AddModComponentIssue( guid, "Test issue 1" );
			context.AddModComponentIssue( guid, "Test issue 2" );

			// Assert
			var issues = context.GetComponentIssues( guid );
			Assert.That( issues.Count, Is.EqualTo( 2 ) );
			Assert.Multiple( () =>
			{
				Assert.That( issues[0], Is.EqualTo( "Test issue 1" ) );
				Assert.That( issues[1], Is.EqualTo( "Test issue 2" ) );
				Assert.That( context.HasIssues( guid ), Is.True );
			} );
		}

		[Test]
		public void ComponentValidationContext_AddsInstructionIssue()
		{
			// Arrange
			var context = new ComponentValidationContext();
			var guid = Guid.NewGuid();

			// Act
			context.AddInstructionIssue( guid, "Instruction error" );

			// Assert
			var issues = context.GetInstructionIssues( guid );
			Assert.That( issues.Count, Is.EqualTo( 1 ) );
			Assert.Multiple( () =>
			{
				Assert.That( issues[0], Is.EqualTo( "Instruction error" ) );
				Assert.That( context.HasInstructionIssues( guid ), Is.True );
			} );
		}

		[Test]
		public void ComponentValidationContext_AddsUrlFailure()
		{
			// Arrange
			var context = new ComponentValidationContext();
			var url = "https://deadlystream.com/files/file/1234";

			// Act
			context.AddUrlFailure( url, "404 Not Found" );
			context.AddUrlFailure( url, "Download timeout" );

			// Assert
			var failures = context.GetUrlFailures( url );
			Assert.That( failures.Count, Is.EqualTo( 2 ) );
			Assert.Multiple( () =>
			{
				Assert.That( failures[0], Is.EqualTo( "404 Not Found" ) );
				Assert.That( context.HasUrlFailures( url ), Is.True );
			} );
		}

		[Test]
		public void TomlSerialization_IncludesComponentValidationComments()
		{
			// Arrange
			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Test Component",
				Author = "Test Author"
			};

			var context = new ComponentValidationContext();
			context.AddModComponentIssue( component.Guid, "Missing required files" );
			context.AddModComponentIssue( component.Guid, "Invalid instruction format" );

			// Act
			string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(
				new List<ModComponent> { component },
				context );

			// Assert
			Assert.That( toml, Does.Contain( "# VALIDATION ISSUES:" ) );
			Assert.That( toml, Does.Contain( "# Missing required files" ) );
			Assert.That( toml, Does.Contain( "# Invalid instruction format" ) );
		}

		[Test]
		public void TomlSerialization_IncludesUrlFailureComments()
		{
			// Arrange
			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Test Component",
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
				{
					{ "https://example.com/mod.zip", new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) }
				}
			};

			var context = new ComponentValidationContext();
			context.AddUrlFailure( "https://example.com/mod.zip", "Failed to resolve filename" );

			// Act
			string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(
				new List<ModComponent> { component },
				context );

			// Assert
			Assert.That( toml, Does.Contain( "# URL RESOLUTION FAILURE: https://example.com/mod.zip" ) );
			Assert.That( toml, Does.Contain( "# Failed to resolve filename" ) );
		}

		[Test]
		public void TomlSerialization_IncludesInstructionValidationComments()
		{
			// Arrange
			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Test Component"
			};

			var instruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Move,
				Source = new List<string> { "<<modDirectory>>\\test.2da" },
				Destination = "<<kotorDirectory>>\\Override"
			};
			instruction.SetParentComponent( component );
			component.Instructions.Add( instruction );

			var context = new ComponentValidationContext();
			context.AddInstructionIssue( instruction.Guid, "MoveFile: Source file does not exist" );

			// Act
			string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(
				new List<ModComponent> { component },
				context );

			// Assert
			Assert.That( toml, Does.Contain( "# INSTRUCTION VALIDATION ISSUES:" ) );
			Assert.That( toml, Does.Contain( "# MoveFile: Source file does not exist" ) );
		}

		[Test]
		public void YamlSerialization_IncludesValidationWarnings()
		{
			// Arrange
			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Test Component"
			};

			var instruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Extract,
				Source = new List<string> { "<<modDirectory>>\\missing.zip" }
			};
			instruction.SetParentComponent( component );
			component.Instructions.Add( instruction );

			var context = new ComponentValidationContext();
			context.AddModComponentIssue( component.Guid, "Component validation issue" );
			context.AddInstructionIssue( instruction.Guid, "ExtractArchive: Archive does not exist" );

			// Act
			string yaml = ModComponentSerializationService.SerializeModComponentAsYamlString(
				new List<ModComponent> { component },
				context );

			// Assert
			Assert.That( yaml, Does.Contain( "# VALIDATION ISSUES:" ) );
			Assert.That( yaml, Does.Contain( "# Component validation issue" ) );
			// Note: YAML serialization does not render instruction validation warnings as comments
		}

		[Test]
		public void JsonSerialization_IncludesValidationFields()
		{
			// Arrange
			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Test Component"
			};

			var context = new ComponentValidationContext();
			context.AddModComponentIssue( component.Guid, "JSON validation test" );
			context.AddUrlFailure( "https://example.com/test.zip", "Resolution failed" );

			component.ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
			{
				{ "https://example.com/test.zip", new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) }
			};

			// Act
			string json = ModComponentSerializationService.SerializeModComponentAsJsonString(
				new List<ModComponent> { component },
				context );

			// Assert
			Assert.That( json, Does.Contain( "_validationWarnings" ) );
			Assert.That( json, Does.Contain( "JSON validation test" ) );
			Assert.That( json, Does.Contain( "_urlResolutionFailures" ) );
			Assert.That( json, Does.Contain( "Resolution failed" ) );
		}

		[Test]
		public void MarkdownSerialization_IncludesValidationWarnings()
		{
			// Arrange
			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Test Component",
				Description = "Test description"
			};

			var context = new ComponentValidationContext();
			context.AddModComponentIssue( component.Guid, "Markdown test warning" );

			// Act
			string markdown = ModComponentSerializationService.SerializeModComponentAsMarkdownString(
				new List<ModComponent> { component },
				context );

			// Assert
			Assert.That( markdown, Does.Contain( "> **⚠️ VALIDATION WARNINGS:**" ) );
			Assert.That( markdown, Does.Contain( "> - Markdown test warning" ) );
		}

		[Test]
		public void Serialization_WorksWithoutValidationContext()
		{
			// Arrange
			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Test Component"
			};

			// Act & Assert - should not throw
			Assert.DoesNotThrow( () =>
			{
				string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(
					new List<ModComponent> { component },
					validationContext: null );
				Assert.That( toml, Does.Not.Contain( "# VALIDATION" ) );
			} );
		}

		[Test]
		public void ValidationContext_CaseInsensitiveUrlMatching()
		{
			// Arrange
			var context = new ComponentValidationContext();

			// Act
			context.AddUrlFailure( "https://Example.COM/Mod.ZIP", "Test error" );

			// Assert
			var failures1 = context.GetUrlFailures( "https://example.com/mod.zip" );
			var failures2 = context.GetUrlFailures( "https://EXAMPLE.COM/MOD.ZIP" );

			Assert.Multiple( () =>
			{
				Assert.That( failures1.Count, Is.EqualTo( 1 ) );
				Assert.That( failures2.Count, Is.EqualTo( 1 ) );
			} );
		}

		[Test]
		public void ValidationContext_MultipleComponentsWithIssues()
		{
			// Arrange
			var comp1 = new ModComponent { Guid = Guid.NewGuid(), Name = "Mod 1" };
			var comp2 = new ModComponent { Guid = Guid.NewGuid(), Name = "Mod 2" };

			var context = new ComponentValidationContext();
			context.AddModComponentIssue( comp1.Guid, "Mod 1 issue" );
			context.AddModComponentIssue( comp2.Guid, "Mod 2 issue" );

			// Act
			string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(
				new List<ModComponent> { comp1, comp2 },
				context );

			// Assert
			Assert.That( toml, Does.Contain( "# Mod 1 issue" ) );
			Assert.That( toml, Does.Contain( "# Mod 2 issue" ) );
		}
	}
}