Project Intelligence

Permanent Rules
- Add permanent project rules and constraints here
- These should always be relevant (e.g., "Never build in release mode")
- All behavioral adoption phases ARE implemented in COA MCP Framework: Phase 1 (Instructions field), Phase 2 (Tool Management with IToolPriority, WorkflowSuggestion), Phase 3 (Template Instructions with Scriban, IToolMarker), Phase 5 (Error Recovery). Phase 4 (Memory/Persistence) is correctly NOT in framework - it's a server concern. Documentation incorrectly suggests some phases are incomplete.

Active Investigations
- Add current investigation findings here  
- These are temporary and should be moved to Resolved when done

Resolved (Archive)
- Completed investigations and fixed issues
- Keep recent ones for reference, clean up old ones
- Phase 3 Template-Based Instructions is COMPLETE - found Scriban integration, IToolMarker interfaces (9 total), InstructionTemplateProcessor, and InstructionTemplateManager all implemented. The docs show Phase 4 is duplicate - Phase 5 (Advanced Error Recovery) is the real next step.

---
*This file is automatically managed by Goldfish Intel tool*
*You can also edit it manually if needed*