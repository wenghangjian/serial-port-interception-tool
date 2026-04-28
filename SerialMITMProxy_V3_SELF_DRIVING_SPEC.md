# Serial MITM Proxy GUI --- V3 Self‑Driving Implementation Specification

## AI Autonomous Development Contract

------------------------------------------------------------------------

## 0. ROLE DEFINITION

You are an autonomous multi‑agent software engineering system.

You MUST: - Plan - Implement - Test - Refactor - Validate - Package

WITHOUT asking architectural questions.

Follow this specification strictly.

------------------------------------------------------------------------

## 1. TARGET

Build a production‑ready Windows application:

**Serial MITM Proxy GUI (V3)**

Capabilities: - Serial MITM forwarding - Monitoring (HEX/ASCII) -
Interactive interception - Rule‑based modification - Injection & delay -
Capture & replay - Plugin extensibility

------------------------------------------------------------------------

## 2. TECHNOLOGY LOCK

OS: Windows 10+\
Language: C#\
Runtime: .NET 8\
UI: WPF + MVVM\
Async Model: async/await + Channels\
Testing: xUnit\
Serialization: System.Text.Json

DO NOT substitute technologies.

------------------------------------------------------------------------

## 3. AUTONOMOUS DEVELOPMENT MODEL

### Agents

  Agent            Responsibility
  ---------------- -----------------------
  ArchitectAgent   Validate structure
  CoreAgent        Proxy engine
  IOAgent          Serial transport
  RuleAgent        Rule engine
  UIAgent          WPF MVVM
  TestAgent        Unit & stress tests
  PerfAgent        Throughput validation
  PackagingAgent   Release build

Agents MUST work sequentially by phase.

------------------------------------------------------------------------

## 4. REPOSITORY STRUCTURE

SerialMitmProxy.sln

src/ SerialMitmProxy.App/ SerialMitmProxy.Application/
SerialMitmProxy.Core/ SerialMitmProxy.Infrastructure/
SerialMitmProxy.Plugins/

tests/ SerialMitmProxy.Core.Tests/

------------------------------------------------------------------------

## 5. DEVELOPMENT PHASES

### PHASE 1 --- Skeleton

Create: - solution - projects - dependency graph - build success

Exit condition: - compiles - CI build passes

------------------------------------------------------------------------

### PHASE 2 --- Serial Transport

Implement:

ISerialEndpoint OpenAsync() ReadLoop() WriteAsync()

Rules: - dedicated read loop - single writer queue - cancellation token
support

Exit: loopback test passes

------------------------------------------------------------------------

### PHASE 3 --- Proxy Engine

Implement ProxySession:

EndpointA ↔ Pipeline ↔ EndpointB

Requirements: - bidirectional - no UI dependency - event emission

Exit: data forwarding verified

------------------------------------------------------------------------

### PHASE 4 --- Frame Pipeline

Pipeline:

bytes → decoder → rule engine → action → write queue → capture

Implement decoders: - TimeSlice - Delimiter - FixedLength

Exit: frames generated deterministically

------------------------------------------------------------------------

### PHASE 5 --- Rule Engine

Matchers: - Direction - Length - HexPattern - Regex

Actions: PASS DROP MODIFY INTERCEPT DELAY INJECT DUPLICATE

Transformers: ReplaceBytes PatchOffset ChecksumFix

Exit: rule test suite passes

------------------------------------------------------------------------

### PHASE 6 --- Intercept System

Create InterceptManager.

Behavior: - intercepted frame pauses pipeline - UI decision required

Commands: Forward Drop EditAndForward Repeat Inject

Exit: manual decision resumes flow

------------------------------------------------------------------------

### PHASE 7 --- Capture & Replay

Files: capture.bin capture.idx

Replay: - original timing - speed factor - single step

Exit: capture roundtrip equality verified

------------------------------------------------------------------------

### PHASE 8 --- UI Layer

Modules: - Session Manager - Live Monitor - Intercept Queue - Rule
Editor - Replay Controller

Constraints: - virtualization required - UI throttling ≤100ms

Exit: UI responsive under load

------------------------------------------------------------------------

### PHASE 9 --- Performance Validation

Target: ≥ 2MB/s sustained throughput

Stress: 1 hour continuous transfer

Exit: no crash no frame loss

------------------------------------------------------------------------

### PHASE 10 --- Packaging

Produce: - Release build - single‑folder deployment - config template

Exit: clean machine launch success

------------------------------------------------------------------------

## 6. THREADING CONTRACT

Allowed: - Channels - background workers - async pipelines

Forbidden: - UI thread blocking - cross‑thread UI access - global
mutable singleton state

------------------------------------------------------------------------

## 7. TEST REQUIREMENTS

Unit: - decoder correctness - rule execution - transformer integrity

Integration: - bidirectional forwarding - intercept workflow

Stress: - high throughput replay

------------------------------------------------------------------------

## 8. AUTONOMOUS VALIDATION LOOP

After each phase:

1.  Build
2.  Run tests
3.  Fix failures
4.  Refactor
5.  Continue

Never skip validation.

------------------------------------------------------------------------

## 9. DEFINITION OF DONE

Application MUST:

✓ Forward serial data\
✓ Display HEX/ASCII\
✓ Intercept frames\
✓ Modify payload\
✓ Replay capture\
✓ Run 1h stress without crash

------------------------------------------------------------------------

## 10. EXECUTION COMMAND

Begin at PHASE 1. Continue automatically until PHASE 10 completion.

DO NOT STOP for clarification.

END SPEC
