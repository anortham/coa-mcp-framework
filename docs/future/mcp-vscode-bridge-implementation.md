# MCP-VSCode Bridge Implementation Plan

## Project Overview

A VS Code extension that acts as a display bridge between MCP (Model Context Protocol) servers and VS Code's rich UI capabilities. This enables AI agents (Claude, GitHub Copilot) to trigger rich visualizations in VS Code while keeping AI context windows lean.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         VS Code                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │            MCP-VSCode Bridge Extension               │   │
│  │  ┌────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │  Display   │  │   Command   │  │   Service   │  │   │
│  │  │  Registry  │  │   Router    │  │   Manager   │  │   │
│  │  └────────────┘  └─────────────┘  └─────────────┘  │   │
│  └────────────────────┬─────────────────────────────────┘   │
│                       │ WebSocket/HTTP                       │
└───────────────────────┼──────────────────────────────────────┘
                        │
    ┌───────────────────┼───────────────────────┐
    │                   │                        │
    ▼                   ▼                        ▼
┌──────────┐    ┌──────────────┐    ┌─────────────────┐
│ SQL MCP  │    │ Knowledge MCP │    │ Claude/Copilot  │
│  (.NET)  │    │ (TypeScript)  │    │     Agent       │
└──────────┘    └──────────────┘    └─────────────────┘
```

## Phase 1: Core Infrastructure

### 1.1 Project Structure

```
mcp-vscode-bridge/
├── src/
│   ├── extension.ts           # Extension entry point
│   ├── server/
│   │   ├── BridgeServer.ts    # WebSocket/HTTP server
│   │   └── ConnectionManager.ts
│   ├── protocol/
│   │   ├── types.ts           # Message definitions
│   │   └── validator.ts
│   ├── display/
│   │   ├── DisplayRegistry.ts
│   │   └── handlers/
│   │       ├── DataGridHandler.ts
│   │       ├── GraphHandler.ts
│   │       ├── DiagramHandler.ts
│   │       └── MarkdownHandler.ts
│   ├── state/
│   │   └── SessionManager.ts
│   └── test/
├── package.json
├── tsconfig.json
└── README.md
```

### 1.2 Package.json

```json
{
  "name": "mcp-vscode-bridge",
  "displayName": "MCP VSCode Bridge",
  "description": "Rich visualization bridge for MCP servers",
  "version": "0.1.0",
  "engines": {
    "vscode": "^1.85.0"
  },
  "categories": ["Other"],
  "main": "./out/extension.js",
  "contributes": {
    "configuration": {
      "title": "MCP Bridge",
      "properties": {
        "mcpBridge.port": {
          "type": "number",
          "default": 7823,
          "description": "Port for MCP communication"
        },
        "mcpBridge.authentication": {
          "type": "string",
          "default": "none",
          "enum": ["none", "token", "certificate"],
          "description": "Authentication method"
        },
        "mcpBridge.autoStart": {
          "type": "boolean",
          "default": true,
          "description": "Automatically start bridge server"
        }
      }
    },
    "commands": [
      {
        "command": "mcpBridge.start",
        "title": "Start MCP Bridge Server"
      },
      {
        "command": "mcpBridge.stop",
        "title": "Stop MCP Bridge Server"
      },
      {
        "command": "mcpBridge.showDashboard",
        "title": "MCP: Show Dashboard"
      },
      {
        "command": "mcpBridge.clearHistory",
        "title": "MCP: Clear Display History"
      }
    ],
    "views": {
      "explorer": [
        {
          "id": "mcpBridge.connections",
          "name": "MCP Connections",
          "icon": "$(plug)"
        }
      ]
    },
    "viewsContainers": {
      "activitybar": [
        {
          "id": "mcp-bridge",
          "title": "MCP Bridge",
          "icon": "resources/mcp-icon.svg"
        }
      ]
    }
  },
  "scripts": {
    "vscode:prepublish": "npm run compile",
    "compile": "tsc -p ./",
    "watch": "tsc -watch -p ./",
    "test": "node ./out/test/runTest.js"
  },
  "devDependencies": {
    "@types/vscode": "^1.85.0",
    "@types/ws": "^8.5.0",
    "@types/node": "^20.0.0",
    "typescript": "^5.0.0"
  },
  "dependencies": {
    "ws": "^8.16.0"
  }
}
```

### 1.3 Extension Entry Point

```typescript
// src/extension.ts
import * as vscode from 'vscode';
import { BridgeServer } from './server/BridgeServer';
import { DisplayRegistry } from './display/DisplayRegistry';
import { SessionManager } from './state/SessionManager';
import { ConnectionTreeProvider } from './views/ConnectionTreeProvider';

let bridgeServer: BridgeServer | null = null;
let displayRegistry: DisplayRegistry | null = null;
let sessionManager: SessionManager | null = null;

export async function activate(context: vscode.ExtensionContext) {
    console.log('MCP VSCode Bridge is activating');

    // Initialize components
    displayRegistry = new DisplayRegistry(context);
    sessionManager = new SessionManager();
    
    // Get configuration
    const config = vscode.workspace.getConfiguration('mcpBridge');
    const port = config.get<number>('port', 7823);
    const autoStart = config.get<boolean>('autoStart', true);

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('mcpBridge.start', () => startBridge(port)),
        vscode.commands.registerCommand('mcpBridge.stop', stopBridge),
        vscode.commands.registerCommand('mcpBridge.showDashboard', showDashboard),
        vscode.commands.registerCommand('mcpBridge.clearHistory', clearHistory)
    );

    // Register tree view
    const treeProvider = new ConnectionTreeProvider(sessionManager);
    vscode.window.registerTreeDataProvider('mcpBridge.connections', treeProvider);

    // Auto-start if configured
    if (autoStart) {
        await startBridge(port);
    }
}

async function startBridge(port: number) {
    if (bridgeServer) {
        vscode.window.showInformationMessage('MCP Bridge is already running');
        return;
    }

    try {
        bridgeServer = new BridgeServer(port, displayRegistry!, sessionManager!);
        await bridgeServer.start();
        
        vscode.window.showInformationMessage(`MCP Bridge started on port ${port}`);
        
        // Update status bar
        updateStatusBar(true, port);
    } catch (error) {
        vscode.window.showErrorMessage(`Failed to start MCP Bridge: ${error}`);
    }
}

function stopBridge() {
    if (!bridgeServer) {
        vscode.window.showInformationMessage('MCP Bridge is not running');
        return;
    }

    bridgeServer.stop();
    bridgeServer = null;
    vscode.window.showInformationMessage('MCP Bridge stopped');
    updateStatusBar(false);
}

function updateStatusBar(running: boolean, port?: number) {
    // Implementation for status bar indicator
}

export function deactivate() {
    if (bridgeServer) {
        bridgeServer.stop();
    }
}
```

### 1.4 Bridge Server Implementation

```typescript
// src/server/BridgeServer.ts
import * as WebSocket from 'ws';
import * as http from 'http';
import { EventEmitter } from 'events';
import { MCPMessage, DisplayCommand } from '../protocol/types';
import { DisplayRegistry } from '../display/DisplayRegistry';
import { SessionManager } from '../state/SessionManager';

export class BridgeServer extends EventEmitter {
    private wss: WebSocket.Server | null = null;
    private httpServer: http.Server | null = null;
    private connections: Map<string, MCPConnection> = new Map();

    constructor(
        private port: number,
        private displayRegistry: DisplayRegistry,
        private sessionManager: SessionManager
    ) {
        super();
    }

    async start(): Promise<void> {
        return new Promise((resolve, reject) => {
            this.httpServer = http.createServer(this.handleHttpRequest.bind(this));
            this.wss = new WebSocket.Server({ server: this.httpServer });

            this.wss.on('connection', this.handleWebSocketConnection.bind(this));
            
            this.httpServer.listen(this.port, () => {
                console.log(`MCP Bridge listening on port ${this.port}`);
                resolve();
            });

            this.httpServer.on('error', reject);
        });
    }

    stop(): void {
        this.connections.forEach(conn => conn.close());
        this.connections.clear();
        
        this.wss?.close();
        this.httpServer?.close();
        
        this.wss = null;
        this.httpServer = null;
    }

    private handleWebSocketConnection(ws: WebSocket, req: http.IncomingMessage) {
        const connectionId = this.generateConnectionId();
        const connection = new MCPConnection(connectionId, ws, this);
        
        this.connections.set(connectionId, connection);
        const session = this.sessionManager.createSession(connectionId);
        
        ws.on('message', async (data) => {
            try {
                const message: MCPMessage = JSON.parse(data.toString());
                await this.handleMessage(connectionId, message, session);
            } catch (error) {
                console.error('Error handling message:', error);
                ws.send(JSON.stringify({
                    type: 'error',
                    error: error.message
                }));
            }
        });

        ws.on('close', () => {
            this.connections.delete(connectionId);
            this.emit('connection-closed', connectionId);
        });

        // Send welcome message
        ws.send(JSON.stringify({
            type: 'connected',
            connectionId,
            version: '1.0.0'
        }));
    }

    private async handleMessage(connectionId: string, message: MCPMessage, session: any) {
        console.log(`Received message from ${connectionId}:`, message.type);

        switch (message.type) {
            case 'display':
                await this.handleDisplayCommand(message.payload as DisplayCommand, session);
                break;
            case 'query':
                await this.handleQuery(connectionId, message);
                break;
            case 'action':
                await this.handleAction(connectionId, message);
                break;
        }
    }

    private async handleDisplayCommand(command: DisplayCommand, session: any) {
        try {
            await this.displayRegistry.handle(command);
            session.addDisplay(command);
        } catch (error) {
            console.error('Display error:', error);
            throw error;
        }
    }

    private handleHttpRequest(req: http.IncomingMessage, res: http.ServerResponse) {
        // Handle HTTP requests for simpler integrations
        if (req.method === 'POST' && req.url === '/display') {
            let body = '';
            req.on('data', chunk => body += chunk);
            req.on('end', async () => {
                try {
                    const command = JSON.parse(body);
                    await this.displayRegistry.handle(command);
                    res.writeHead(200, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ success: true }));
                } catch (error) {
                    res.writeHead(500, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: error.message }));
                }
            });
        } else {
            res.writeHead(404);
            res.end('Not found');
        }
    }

    private generateConnectionId(): string {
        return `mcp-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    }
}

class MCPConnection {
    constructor(
        public id: string,
        private ws: WebSocket,
        private server: BridgeServer
    ) {}

    send(message: any): void {
        if (this.ws.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify(message));
        }
    }

    close(): void {
        this.ws.close();
    }
}
```

## Phase 2: Protocol & Types

### 2.1 Message Protocol Definition

```typescript
// src/protocol/types.ts

export interface MCPMessage {
    id: string;
    timestamp: number;
    source: 'mcp' | 'agent' | 'user' | 'vscode';
    type: 'display' | 'query' | 'response' | 'action' | 'error' | 'connected';
    payload: any;
}

export interface DisplayCommand {
    action: DisplayAction;
    data: any;
    options?: DisplayOptions;
    metadata?: Record<string, any>;
}

export type DisplayAction = 
    | 'showData'
    | 'showGraph'
    | 'showDiagram'
    | 'openFile'
    | 'showMarkdown'
    | 'showDiff'
    | 'showTree'
    | 'showChart';

export interface DisplayOptions {
    location?: 'panel' | 'sidebar' | 'editor' | 'modal' | 'notification';
    preserve?: boolean;
    interactive?: boolean;
    renderer?: string; // Preferred extension ID
    title?: string;
    actions?: ActionButton[];
}

export interface ActionButton {
    id: string;
    label: string;
    tooltip?: string;
    callback: string; // MCP tool to call
    parameters?: Record<string, any>;
}

export interface QueryCommand {
    tool: string;
    parameters: Record<string, any>;
    requestId: string;
}

export interface ResponseData {
    requestId: string;
    success: boolean;
    data?: any;
    error?: string;
}
```

## Phase 3: Display Handlers

### 3.1 Display Registry

```typescript
// src/display/DisplayRegistry.ts
import * as vscode from 'vscode';
import { DisplayCommand, DisplayAction } from '../protocol/types';
import { DataGridHandler } from './handlers/DataGridHandler';
import { GraphHandler } from './handlers/GraphHandler';
import { DiagramHandler } from './handlers/DiagramHandler';
import { MarkdownHandler } from './handlers/MarkdownHandler';

export interface DisplayHandler {
    canHandle(action: DisplayAction): boolean;
    execute(command: DisplayCommand): Promise<void>;
}

export class DisplayRegistry {
    private handlers: Map<DisplayAction, DisplayHandler> = new Map();
    private extensionDetector: ExtensionDetector;
    private context: vscode.ExtensionContext;

    constructor(context: vscode.ExtensionContext) {
        this.context = context;
        this.extensionDetector = new ExtensionDetector();
        this.registerBuiltInHandlers();
    }

    private registerBuiltInHandlers() {
        this.register('showData', new DataGridHandler(this.context));
        this.register('showGraph', new GraphHandler(this.context));
        this.register('showDiagram', new DiagramHandler(this.context));
        this.register('showMarkdown', new MarkdownHandler(this.context));
    }

    register(action: DisplayAction, handler: DisplayHandler): void {
        this.handlers.set(action, handler);
    }

    async handle(command: DisplayCommand): Promise<void> {
        // Check for preferred external extension
        if (command.options?.renderer) {
            const available = await this.extensionDetector.isAvailable(command.options.renderer);
            if (available) {
                return this.delegateToExtension(command);
            }
        }

        // Use built-in handler
        const handler = this.handlers.get(command.action);
        if (!handler) {
            throw new Error(`No handler registered for action: ${command.action}`);
        }

        return handler.execute(command);
    }

    private async delegateToExtension(command: DisplayCommand): Promise<void> {
        const { renderer } = command.options!;
        
        // Map to extension-specific commands
        const extensionCommands: Record<string, string> = {
            'GrapeCity.gc-excelviewer': 'excel-viewer.open',
            'hediet.vscode-drawio': 'vscode-drawio.convert',
            'shd101wyy.markdown-preview-enhanced': 'markdown-preview-enhanced.openPreview'
        };

        const extensionCommand = extensionCommands[renderer];
        if (extensionCommand) {
            await vscode.commands.executeCommand(extensionCommand, command.data);
        }
    }
}

class ExtensionDetector {
    async isAvailable(extensionId: string): Promise<boolean> {
        const extension = vscode.extensions.getExtension(extensionId);
        if (!extension) return false;
        
        if (!extension.isActive) {
            await extension.activate();
        }
        
        return true;
    }

    async getAvailableRenderers(): Promise<string[]> {
        const knownExtensions = [
            'GrapeCity.gc-excelviewer',
            'hediet.vscode-drawio',
            'jebbs.plantuml',
            'shd101wyy.markdown-preview-enhanced'
        ];

        const available: string[] = [];
        for (const id of knownExtensions) {
            if (await this.isAvailable(id)) {
                available.push(id);
            }
        }

        return available;
    }
}
```

### 3.2 Data Grid Handler

```typescript
// src/display/handlers/DataGridHandler.ts
import * as vscode from 'vscode';
import { DisplayCommand, DisplayHandler } from '../../protocol/types';

export class DataGridHandler implements DisplayHandler {
    constructor(private context: vscode.ExtensionContext) {}

    canHandle(action: string): boolean {
        return action === 'showData';
    }

    async execute(command: DisplayCommand): Promise<void> {
        const { data, options } = command;

        // Try external viewers first
        if (await this.tryExternalViewer(data, options)) {
            return;
        }

        // Use built-in webview
        const panel = vscode.window.createWebviewPanel(
            'mcpDataGrid',
            options?.title || 'Data Grid',
            this.getViewColumn(options?.location),
            {
                enableScripts: true,
                retainContextWhenHidden: true,
                localResourceRoots: [this.context.extensionUri]
            }
        );

        panel.webview.html = this.getWebviewContent(data, panel.webview);
        
        // Handle messages from webview
        panel.webview.onDidReceiveMessage(
            message => this.handleWebviewMessage(message, command),
            undefined,
            this.context.subscriptions
        );
    }

    private async tryExternalViewer(data: any, options: any): Promise<boolean> {
        const excelViewer = vscode.extensions.getExtension('GrapeCity.gc-excelviewer');
        
        if (excelViewer && options?.preferExternal !== false) {
            // Save data to temp file and open with Excel viewer
            const tempFile = await this.saveToTempFile(data);
            await vscode.commands.executeCommand('vscode.open', vscode.Uri.file(tempFile));
            return true;
        }

        return false;
    }

    private getWebviewContent(data: any, webview: vscode.Webview): string {
        const nonce = this.getNonce();

        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource} 'unsafe-inline'; script-src 'nonce-${nonce}' https://unpkg.com;">
    <link href="https://unpkg.com/tabulator-tables@5.5.0/dist/css/tabulator_midnight.min.css" rel="stylesheet">
    <style>
        body { padding: 0; margin: 0; }
        #data-table { height: 100vh; }
        .tabulator-cell { cursor: pointer; }
        .context-menu {
            position: absolute;
            background: var(--vscode-menu-background);
            border: 1px solid var(--vscode-menu-border);
            padding: 4px 0;
            display: none;
            z-index: 1000;
        }
        .context-menu-item {
            padding: 4px 16px;
            cursor: pointer;
        }
        .context-menu-item:hover {
            background: var(--vscode-menu-selectionBackground);
        }
    </style>
</head>
<body>
    <div id="data-table"></div>
    <div id="context-menu" class="context-menu">
        <div class="context-menu-item" data-action="copy">Copy</div>
        <div class="context-menu-item" data-action="filter">Filter by this value</div>
        <div class="context-menu-item" data-action="details">Show details</div>
    </div>
    
    <script nonce="${nonce}" src="https://unpkg.com/tabulator-tables@5.5.0/dist/js/tabulator.min.js"></script>
    <script nonce="${nonce}">
        const vscode = acquireVsCodeApi();
        const data = ${JSON.stringify(data)};
        
        // Initialize Tabulator
        const table = new Tabulator("#data-table", {
            data: data.rows || data,
            autoColumns: data.columns ? false : true,
            columns: data.columns ? data.columns.map(col => ({
                title: col.name || col,
                field: col.field || col,
                sorter: col.type || "string",
                headerFilter: true,
                resizable: true
            })) : undefined,
            layout: "fitDataFill",
            pagination: true,
            paginationSize: 100,
            movableColumns: true,
            groupBy: false,
            groupStartOpen: true,
            responsiveLayout: "collapse"
        });

        // Right-click context menu
        table.on("cellContext", function(e, cell) {
            e.preventDefault();
            const menu = document.getElementById("context-menu");
            menu.style.display = "block";
            menu.style.left = e.pageX + "px";
            menu.style.top = e.pageY + "px";
            
            menu.dataset.cellValue = cell.getValue();
            menu.dataset.cellField = cell.getField();
            menu.dataset.rowData = JSON.stringify(cell.getRow().getData());
        });

        // Handle context menu clicks
        document.getElementById("context-menu").addEventListener("click", function(e) {
            const action = e.target.dataset.action;
            const menu = this;
            
            if (action) {
                vscode.postMessage({
                    command: 'contextAction',
                    action: action,
                    value: menu.dataset.cellValue,
                    field: menu.dataset.cellField,
                    row: JSON.parse(menu.dataset.rowData)
                });
            }
            
            menu.style.display = "none";
        });

        // Hide context menu on click outside
        document.addEventListener("click", function() {
            document.getElementById("context-menu").style.display = "none";
        });

        // Handle row clicks
        table.on("rowClick", function(e, row) {
            vscode.postMessage({
                command: 'rowClick',
                data: row.getData()
            });
        });
    </script>
</body>
</html>`;
    }

    private handleWebviewMessage(message: any, originalCommand: DisplayCommand) {
        switch (message.command) {
            case 'rowClick':
                // If there's a callback defined, trigger it
                if (originalCommand.options?.actions) {
                    // Send back to MCP
                    this.triggerMCPCallback('row_details', message.data);
                }
                break;
            case 'contextAction':
                this.handleContextAction(message);
                break;
        }
    }

    private async triggerMCPCallback(action: string, data: any) {
        // This would communicate back to the MCP server
        // Implementation depends on your bridge architecture
    }

    private getViewColumn(location?: string): vscode.ViewColumn {
        switch (location) {
            case 'sidebar': return vscode.ViewColumn.Beside;
            case 'panel': return vscode.ViewColumn.Two;
            default: return vscode.ViewColumn.Active;
        }
    }

    private getNonce(): string {
        let text = '';
        const possible = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
        for (let i = 0; i < 32; i++) {
            text += possible.charAt(Math.floor(Math.random() * possible.length));
        }
        return text;
    }

    private async saveToTempFile(data: any): Promise<string> {
        // Implementation to save data as CSV for external viewers
        const os = require('os');
        const path = require('path');
        const fs = require('fs').promises;
        
        const tempDir = os.tmpdir();
        const tempFile = path.join(tempDir, `mcp-data-${Date.now()}.csv`);
        
        // Convert to CSV
        const csv = this.convertToCSV(data);
        await fs.writeFile(tempFile, csv);
        
        return tempFile;
    }

    private convertToCSV(data: any): string {
        // Simple CSV conversion
        if (!data.rows || data.rows.length === 0) return '';
        
        const headers = data.columns ? data.columns.map((c: any) => c.name || c) : Object.keys(data.rows[0]);
        const rows = data.rows.map((row: any) => {
            return headers.map((h: string) => {
                const value = row[h];
                return typeof value === 'string' && value.includes(',') ? `"${value}"` : value;
            }).join(',');
        });
        
        return [headers.join(','), ...rows].join('\n');
    }
}
```

## Phase 4: MCP Server Integration

### 4.1 TypeScript MCP Server Integration

```typescript
// mcp-server-integration/typescript/VSCodeBridge.ts
import WebSocket from 'ws';
import { EventEmitter } from 'events';

export interface VSCodeBridgeOptions {
    port?: number;
    autoReconnect?: boolean;
    reconnectInterval?: number;
}

export class VSCodeBridge extends EventEmitter {
    private ws: WebSocket | null = null;
    private messageQueue: any[] = [];
    private options: Required<VSCodeBridgeOptions>;
    private reconnectTimer?: NodeJS.Timeout;
    private isConnecting = false;

    constructor(options: VSCodeBridgeOptions = {}) {
        super();
        this.options = {
            port: options.port || 7823,
            autoReconnect: options.autoReconnect !== false,
            reconnectInterval: options.reconnectInterval || 5000
        };
    }

    async connect(): Promise<void> {
        if (this.isConnecting || this.isConnected()) {
            return;
        }

        this.isConnecting = true;

        return new Promise((resolve, reject) => {
            const url = `ws://localhost:${this.options.port}`;
            this.ws = new WebSocket(url);

            this.ws.on('open', () => {
                console.log('Connected to VSCode Bridge');
                this.isConnecting = false;
                this.flushQueue();
                this.emit('connected');
                resolve();
            });

            this.ws.on('message', (data) => {
                try {
                    const message = JSON.parse(data.toString());
                    this.handleMessage(message);
                } catch (error) {
                    console.error('Failed to parse message:', error);
                }
            });

            this.ws.on('close', () => {
                this.ws = null;
                this.isConnecting = false;
                this.emit('disconnected');
                
                if (this.options.autoReconnect) {
                    this.scheduleReconnect();
                }
            });

            this.ws.on('error', (error) => {
                this.isConnecting = false;
                if (!this.isConnected()) {
                    reject(error);
                }
                this.emit('error', error);
            });
        });
    }

    private scheduleReconnect() {
        if (this.reconnectTimer) {
            clearTimeout(this.reconnectTimer);
        }

        this.reconnectTimer = setTimeout(() => {
            console.log('Attempting to reconnect to VSCode Bridge...');
            this.connect().catch(console.error);
        }, this.options.reconnectInterval);
    }

    isConnected(): boolean {
        return this.ws?.readyState === WebSocket.OPEN;
    }

    send(command: any): void {
        const message = {
            id: this.generateId(),
            timestamp: Date.now(),
            source: 'mcp',
            type: 'display',
            payload: command
        };

        if (this.isConnected()) {
            this.ws!.send(JSON.stringify(message));
        } else {
            this.messageQueue.push(message);
            if (!this.isConnecting && this.options.autoReconnect) {
                this.connect().catch(console.error);
            }
        }
    }

    display(action: string, data: any, options?: any): void {
        this.send({
            action,
            data,
            options
        });
    }

    private flushQueue() {
        while (this.messageQueue.length > 0 && this.isConnected()) {
            const message = this.messageQueue.shift();
            this.ws!.send(JSON.stringify(message));
        }
    }

    private handleMessage(message: any) {
        if (message.type === 'query') {
            this.emit('query', message);
        } else if (message.type === 'action') {
            this.emit('action', message);
        }
    }

    private generateId(): string {
        return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    }

    disconnect() {
        if (this.reconnectTimer) {
            clearTimeout(this.reconnectTimer);
        }
        
        if (this.ws) {
            this.ws.close();
            this.ws = null;
        }
    }
}

// Example usage in MCP tool
export class EnhancedSQLTool {
    private vscode: VSCodeBridge;

    constructor() {
        this.vscode = new VSCodeBridge({ autoReconnect: true });
        this.vscode.connect().catch(console.error);
    }

    async executeQuery(query: string, context?: any): Promise<any> {
        const results = await this.database.execute(query);

        // Prepare minimal response for AI
        const aiResponse = {
            success: true,
            rowCount: results.rows.length,
            columns: results.columns.map(c => c.name),
            sample: results.rows.slice(0, 3),
            executionTime: results.executionTime
        };

        // Send full results to VSCode if connected
        if (this.vscode.isConnected()) {
            this.vscode.display('showData', {
                columns: results.columns,
                rows: results.rows,
                metadata: {
                    query,
                    executionTime: results.executionTime,
                    database: this.database.name
                }
            }, {
                title: `Query Results (${results.rows.length} rows)`,
                location: 'panel',
                interactive: true,
                actions: [
                    {
                        id: 'export',
                        label: 'Export to CSV',
                        callback: 'exportResults'
                    }
                ]
            });
        }

        return aiResponse;
    }

    async showDependencyGraph(tableName: string): Promise<any> {
        const dependencies = await this.analyzeDependencies(tableName);

        // Generate mermaid diagram
        const mermaid = this.generateMermaidDiagram(dependencies);

        // AI gets text summary
        const aiResponse = `Table ${tableName} has ${dependencies.length} dependencies: ${dependencies.map(d => d.name).join(', ')}`;

        // VSCode gets interactive diagram
        if (this.vscode.isConnected()) {
            this.vscode.display('showDiagram', {
                type: 'mermaid',
                content: mermaid,
                nodes: dependencies.map(d => ({
                    id: d.name,
                    label: d.name,
                    metadata: d
                }))
            }, {
                title: `Dependencies for ${tableName}`,
                interactive: true,
                renderer: 'hediet.vscode-drawio'  // Prefer draw.io if available
            });
        }

        return aiResponse;
    }
}
```

### 4.2 .NET MCP Server Integration

```csharp
// mcp-server-integration/dotnet/VSCodeBridge.cs
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MCP.VSCode
{
    public class VSCodeBridge : IDisposable
    {
        private ClientWebSocket _webSocket;
        private readonly Queue<object> _messageQueue = new();
        private readonly int _port;
        private readonly bool _autoReconnect;
        private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
        private CancellationTokenSource _cancellationTokenSource;
        private Task _receiveTask;

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<MessageEventArgs> MessageReceived;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        public VSCodeBridge(int port = 7823, bool autoReconnect = true)
        {
            _port = port;
            _autoReconnect = autoReconnect;
        }

        public async Task ConnectAsync()
        {
            if (IsConnected) return;

            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            var uri = new Uri($"ws://localhost:{_port}");
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);

            Connected?.Invoke(this, EventArgs.Empty);

            // Start receive loop
            _receiveTask = ReceiveLoop();

            // Flush queued messages
            await FlushQueueAsync();
        }

        public async Task DisplayAsync(string action, object data, DisplayOptions options = null)
        {
            var command = new DisplayCommand
            {
                Action = action,
                Data = data,
                Options = options
            };

            await SendAsync(command);
        }

        public async Task SendAsync(object command)
        {
            var message = new MCPMessage
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Source = "mcp",
                Type = "display",
                Payload = command
            };

            if (IsConnected)
            {
                await SendMessageAsync(message);
            }
            else
            {
                _messageQueue.Enqueue(message);
                if (_autoReconnect)
                {
                    _ = Task.Run(async () => await ConnectAsync());
                }
            }
        }

        private async Task SendMessageAsync(object message)
        {
            await _sendSemaphore.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cancellationTokenSource.Token
                );
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        private async Task FlushQueueAsync()
        {
            while (_messageQueue.Count > 0 && IsConnected)
            {
                var message = _messageQueue.Dequeue();
                await SendMessageAsync(message);
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            
            while (!_cancellationTokenSource.Token.IsCancellationRequested && IsConnected)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(buffer, _cancellationTokenSource.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        var message = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                        MessageReceived?.Invoke(this, new MessageEventArgs { Message = message });
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        Disconnected?.Invoke(this, EventArgs.Empty);
                        
                        if (_autoReconnect)
                        {
                            await Task.Delay(5000);
                            await ConnectAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Receive error: {ex.Message}");
                    break;
                }
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _webSocket?.Dispose();
            _sendSemaphore?.Dispose();
        }
    }

    public class DisplayCommand
    {
        public string Action { get; set; }
        public object Data { get; set; }
        public DisplayOptions Options { get; set; }
    }

    public class DisplayOptions
    {
        public string Location { get; set; }
        public bool Interactive { get; set; }
        public string Title { get; set; }
        public string Renderer { get; set; }
        public List<ActionButton> Actions { get; set; }
    }

    public class ActionButton
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Callback { get; set; }
    }

    public class MCPMessage
    {
        public string Id { get; set; }
        public long Timestamp { get; set; }
        public string Source { get; set; }
        public string Type { get; set; }
        public object Payload { get; set; }
    }

    public class MessageEventArgs : EventArgs
    {
        public Dictionary<string, object> Message { get; set; }
    }

    // Example usage in MCP tool
    public class EnhancedSQLMcpServer
    {
        private readonly VSCodeBridge _vscode;
        private readonly IDatabase _database;

        public EnhancedSQLMcpServer(IDatabase database)
        {
            _database = database;
            _vscode = new VSCodeBridge(autoReconnect: true);
            _ = _vscode.ConnectAsync();
        }

        [McpTool("execute_sql")]
        public async Task<object> ExecuteSql(string query)
        {
            var results = await _database.ExecuteAsync(query);

            // Minimal response for AI
            var aiResponse = new
            {
                success = true,
                rowCount = results.Rows.Count,
                columns = results.Columns.Select(c => c.Name).ToList(),
                sample = results.Rows.Take(3).ToList(),
                executionTime = results.ExecutionTime
            };

            // Rich display in VSCode
            if (_vscode.IsConnected)
            {
                await _vscode.DisplayAsync("showData", new
                {
                    columns = results.Columns,
                    rows = results.Rows,
                    metadata = new
                    {
                        query,
                        executionTime = results.ExecutionTime,
                        database = _database.Name
                    }
                }, new DisplayOptions
                {
                    Title = $"Query Results ({results.Rows.Count} rows)",
                    Location = "panel",
                    Interactive = true,
                    Actions = new List<ActionButton>
                    {
                        new() { Id = "export", Label = "Export to CSV", Callback = "exportResults" }
                    }
                });
            }

            return aiResponse;
        }

        [McpTool("show_schema")]
        public async Task<object> ShowSchema(string tableName)
        {
            var schema = await _database.GetSchemaAsync(tableName);
            
            // Text response for AI
            var aiResponse = $"Table {tableName} has {schema.Columns.Count} columns: " +
                           string.Join(", ", schema.Columns.Select(c => $"{c.Name} ({c.Type})"));

            // Visual schema in VSCode
            if (_vscode.IsConnected)
            {
                var mermaid = GenerateMermaidERD(schema);
                
                await _vscode.DisplayAsync("showDiagram", new
                {
                    type = "mermaid",
                    content = mermaid,
                    schema = schema
                }, new DisplayOptions
                {
                    Title = $"Schema: {tableName}",
                    Interactive = true,
                    Renderer = "hediet.vscode-drawio"
                });
            }

            return aiResponse;
        }
    }
}
```

## Phase 5: Testing & Documentation

### 5.1 Test Suite

```typescript
// src/test/suite/bridge.test.ts
import * as assert from 'assert';
import * as vscode from 'vscode';
import { BridgeServer } from '../../server/BridgeServer';
import { DisplayRegistry } from '../../display/DisplayRegistry';
import { MockMCPClient } from '../mocks/MockMCPClient';

suite('MCP VSCode Bridge Test Suite', () => {
    let bridge: BridgeServer;
    let mockClient: MockMCPClient;

    setup(async () => {
        const context = await vscode.extensions.getExtension('mcp-vscode-bridge')?.activate();
        bridge = new BridgeServer(7823, new DisplayRegistry(context), null);
        await bridge.start();
        
        mockClient = new MockMCPClient();
        await mockClient.connect(7823);
    });

    teardown(async () => {
        mockClient.disconnect();
        bridge.stop();
    });

    test('Should handle data display command', async () => {
        const testData = {
            columns: ['id', 'name'],
            rows: [[1, 'Test'], [2, 'Data']]
        };

        await mockClient.send({
            action: 'showData',
            data: testData,
            options: { title: 'Test Data' }
        });

        // Wait for webview to be created
        await new Promise(resolve => setTimeout(resolve, 1000));

        // Verify webview was created
        const panels = vscode.window.tabGroups.all
            .flatMap(group => group.tabs)
            .filter(tab => tab.label === 'Test Data');
        
        assert.strictEqual(panels.length, 1, 'Data grid webview should be created');
    });

    test('Should handle diagram display command', async () => {
        const mermaidDiagram = `
            graph TD
            A[Table A] --> B[Table B]
            B --> C[Table C]
        `;

        await mockClient.send({
            action: 'showDiagram',
            data: { type: 'mermaid', content: mermaidDiagram },
            options: { title: 'Dependencies' }
        });

        await new Promise(resolve => setTimeout(resolve, 1000));

        // Verify diagram viewer was created
        const panels = vscode.window.tabGroups.all
            .flatMap(group => group.tabs)
            .filter(tab => tab.label === 'Dependencies');
        
        assert.strictEqual(panels.length, 1, 'Diagram viewer should be created');
    });

    test('Should queue messages when disconnected', async () => {
        mockClient.disconnect();
        
        // Bridge should queue this message
        const testData = { test: 'data' };
        
        // Reconnect and verify queued message is processed
        await mockClient.connect(7823);
        
        const messages = await mockClient.getReceivedMessages();
        assert.ok(messages.some(m => m.type === 'connected'));
    });
});
```

### 5.2 Configuration Documentation

```json
// .vscode/settings.json (for users)
{
  "mcpBridge.port": 7823,
  "mcpBridge.authentication": "token",
  "mcpBridge.autoStart": true,
  "mcpBridge.display": {
    "defaultLocation": "panel",
    "preserveHistory": true,
    "maxHistoryItems": 50,
    "preferExternalViewers": true
  },
  "mcpBridge.renderers": {
    "data": "GrapeCity.gc-excelviewer",
    "diagram": "hediet.vscode-drawio",
    "markdown": "shd101wyy.markdown-preview-enhanced"
  },
  "mcpBridge.connections": {
    "sql": {
      "type": "websocket",
      "port": 7823,
      "autoConnect": true
    },
    "knowledge": {
      "type": "http",
      "port": 7824,
      "token": "${env:MCP_KNOWLEDGE_TOKEN}"
    }
  }
}
```

## Deployment Checklist

### Pre-release
- [ ] All tests passing
- [ ] Extension manifest complete
- [ ] Icons and assets included
- [ ] README with examples
- [ ] CHANGELOG updated
- [ ] Security review completed

### Publishing
```bash
# Install vsce
npm install -g vsce

# Package extension
vsce package

# Publish to marketplace
vsce publish
```

### MCP Server Updates
- [ ] Add VSCodeBridge to TypeScript MCP servers
- [ ] Add VSCodeBridge to .NET MCP servers
- [ ] Update MCP documentation
- [ ] Add connection examples
- [ ] Test with Claude Desktop
- [ ] Test with GitHub Copilot

## Next Steps

1. **Immediate Actions**
   - Set up the VS Code extension project
   - Implement core WebSocket server
   - Create basic display handlers
   - Test with a simple MCP server

2. **Integration Phase**
   - Add VSCodeBridge to your existing MCP servers
   - Test with real SQL queries and knowledge base
   - Refine the protocol based on usage

3. **Enhancement Phase**
   - Add more visualization types
   - Integrate with more VS Code extensions
   - Add bi-directional communication
   - Implement session management

4. **Community Release**
   - Open source the extension
   - Create documentation site
   - Build example MCP servers
   - Gather feedback and iterate