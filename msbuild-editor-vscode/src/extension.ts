import * as vscode from 'vscode';
import * as roslyn from './roslynImport/main';
import { workspace, ExtensionContext } from 'vscode';
import { msbuildEditorOptions } from './options';
import { RoslynLanguageServerDefinition } from './roslynImport/lsptoolshost/roslynLanguageServer';
import path from 'path';

export function activate(context: ExtensionContext) {
	const lspDefinition : RoslynLanguageServerDefinition = {
		clientId: 'msbuild-lsp',
		clientName: 'MSBuild LSP',
		clientOptions:  {
			documentSelector: [{ scheme: 'file', language: 'msbuild' }],
			synchronize: {
				fileEvents: [],
			},
			markdown: {
				supportHtml: true
			},
		},
		serverPathEnvVar: 'MSBUILD_LANGUAGE_SERVER_PATH',
		bundledServerPath: path.join(context.extensionPath, 'server', 'MSBuildLanguageServer.dll'),
		commandIdPrefix: 'msbuild'
	};

	roslyn.activate(context, msbuildEditorOptions, lspDefinition);

	console.log('Congratulations, your extension "msbuild-editor" is now active!');

	/*
	let disposable = vscode.commands.registerCommand('msbuild-editor.helloWorld', () => {
		vscode.window.showInformationMessage('Hello World from msbuild-editor!');
	});
	context.subscriptions.push(disposable);
	*/
}

// This method is called when your extension is deactivated
export function deactivate() {
}
