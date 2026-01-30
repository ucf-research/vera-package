# VERA Mock Participant Tester

A testing utility for the VERA trial workflow system that allows creation and management of mock participants for testing trial progression, conditions, and workflow logic.

## Quick Start

1. **Add to Scene:**
   - Add `VERAMockParticipantTester` component to any GameObject in your scene
   - Works alongside `VERALogger` and other VERA components

2. **Basic Configuration:**
   - Enable `createMockParticipantOnStart` to auto-create a participant when the scene starts
   - Leave `overrideParticipantId` empty for auto-generated IDs, or specify a custom ID for testing

3. **Auto-Testing Mode:**
   - Enable `autoAdvanceTrials` to automatically progress through the trial workflow
   - Set `autoAdvanceDelay` to control time between trial transitions
   - Enable `autoCompletTrials` to simulate participants completing tasks

## Features

### Automatic Mock Participant Creation
- Creates mock participants on scene start
- Supports custom participant IDs or auto-generation
- Integrates with existing VERA participant management system

### Trial Workflow Navigation
- Manual or automatic trial progression
- Complete trials programmatically
- Navigate forward through trial sequences

### Between-Subjects Condition Testing
- Set specific condition values for testing
- Format: `conditionName:value` (e.g., `Weather:0`, `Difficulty:2`)
- Apply multiple conditions simultaneously

### Debugging Tools
- Verbose logging of all workflow operations
- Periodic workflow state dumps
- Real-time trial information display

## Inspector Settings

### Mock Participant Settings
- **Create Mock Participant On Start**: Automatically creates a participant when the scene loads
- **Override Participant ID**: Use a specific ID instead of auto-generation (useful for testing specific participant assignments)

### Trial Workflow Testing
- **Auto Advance Trials**: Automatically progress through trials without manual input
- **Auto Advance Delay**: Time in seconds to wait between advancing trials
- **Auto Complete Trials**: Automatically mark trials as complete before advancing

### Manual Between-Subjects Conditions
- **Manual Conditions**: List of conditions in `name:value` format
  - Example: `Weather:0` sets Weather condition to value 0
  - Example: `Difficulty:1` sets Difficulty condition to value 1

### Debugging
- **Verbose Logging**: Enable detailed console logs for all operations
- **Log Workflow State Interval**: Automatically log workflow state every N seconds (0 = disabled)

## Public API Methods

You can call these methods from other scripts or wire them to Unity UI buttons:

```csharp
// Create a mock participant
mockTester.CreateMockParticipant();

// Advance to next trial
mockTester.AdvanceToNextTrial();

// Complete current trial and advance
mockTester.CompleteCurrentTrial();

// Set a specific condition
mockTester.SetCondition("Weather", 0);

// Get current trial info
TrialConfig currentTrial = mockTester.GetCurrentTrial();

// Log current state
mockTester.LogWorkflowState();

// Toggle auto-advance at runtime
mockTester.ToggleAutoAdvance();
```

## Context Menu Options

Right-click the component in the Inspector to access quick actions:
- **Create Mock Participant Now**: Create a participant immediately
- **Advance to Next Trial**: Move to the next trial in the sequence
- **Complete Current Trial**: Mark current trial complete and advance
- **Log Current Workflow State**: Print detailed workflow info to console
- **Toggle Auto-Advance**: Enable/disable automatic trial progression

## Example Use Cases

### 1. Testing a Specific Trial Sequence
```csharp
// In Inspector:
// - autoAdvanceTrials: true
// - autoAdvanceDelay: 2
// - autoCompletTrials: true
// - verboseLogging: true

// This will automatically step through all trials with 2-second delays
```

### 2. Testing Between-Subjects Conditions
```csharp
// In Inspector, add to Manual Conditions list:
// - "Weather:0"
// - "Difficulty:2"
// - "Version:1"

// The mock participant will be assigned these specific condition values
```

### 3. Manual Trial Testing
```csharp
// In Inspector:
// - createMockParticipantOnStart: true
// - autoAdvanceTrials: false
// - verboseLogging: true

// Then use Context Menu or UI buttons to manually control progression
```

### 4. Testing Trial Workflow from API Endpoint
```csharp
// The mock participant will automatically fetch and execute trials from:
// http://localhost:4000/vera-portal/api/experiments/{experimentId}/trials/execution-order

// Configure your experiment ID in VERALogger as usual
```

## Integration with Existing VERA Components

This tester works seamlessly with:
- **VERALogger**: Uses the same participant and workflow managers
- **VERASessionManager**: Subscribes to initialization events
- **VERATrialWorkflowManager**: Controls trial progression
- **VERAParticipantManager**: Creates and manages mock participants

## Logging Output

When `verboseLogging` is enabled, you'll see detailed information like:

```
[Mock Participant Tester] VERA initialized successfully!
[Mock Participant Tester] Setting condition 'Weather' = 0
[Mock Participant Tester] Applied 3 manual conditions
=== VERA Trial Workflow State ===
Participant ID: 42
Participant UUID: abc123...
Participant State: IN_EXPERIMENT
Current Trial: Introduction
  - ID: trial_001
  - Type: standard
  - Order: 0
  - Conditions:
    * Weather = Sunny
    * Difficulty = Hard
================================
[Mock Participant Tester] Completing trial: Introduction
[Mock Participant Tester] Advancing to next trial...
```

## Tips

- Use `logWorkflowStateInterval` to monitor state changes during automated testing
- Combine with `VERADemoConditions.cs` for more complex condition logic
- Wire public methods to UI buttons for manual testing interfaces
- Use context menu options during Play mode for quick testing
- Check the Unity Console for detailed workflow progression logs

## Troubleshooting

**Participant not created:**
- Ensure VERALogger is properly configured in the scene
- Check that the VERA Portal API is accessible
- Verify experiment and site IDs are correct

**Trials not advancing:**
- Enable `verboseLogging` to see detailed progression logs
- Check that the trial workflow was successfully fetched from the API
- Verify the `/api/experiments/{id}/trials/execution-order` endpoint is accessible

**Conditions not applying:**
- Ensure condition names match those defined in the VERA Portal
- Check that conditions are formatted correctly: `name:value`
- Verify conditions are being set after VERA initialization
