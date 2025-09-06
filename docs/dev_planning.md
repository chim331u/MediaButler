
# MediaButler Development Planning Document

## Project Overview

**MediaButler** is an intelligent media file organization system that automatically classifies and organizes video files (TV shows, movies) using machine learning. The system monitors a watch folder, analyzes filenames, and moves files to appropriate library locations.

### Vision
Transform chaotic media downloads into a perfectly organized library through intelligent automation, following Rich Hickey's "Simple Made Easy" philosophy.

### Core Principles (Simple Made Easy)
1. **Simple vs Easy**: Choose simple (un-braided, one-fold) solutions over familiar ones
2. **Avoid Complecting**: Keep concerns separate - no braiding of disparate concepts
3. **Compose, Don't Complex**: Place independent components together without intertwining
4. **Values Over State**: Prefer immutable data and explicit state management
5. **Declarative Over Imperative**: Describe what, not how
6. **Artifact Over Construct**: Judge tools by the simplicity of what they produce

---

## Enhanced Development Strategy

### **Performance-Driven Development (PDD) Pattern**

#### **Daily Performance Budget**
Every development task must validate against these constraints:
```
Memory Usage: <300MB (validated every build)
API Response Time: <100ms (validated every endpoint)
ML Classification: <500ms (validated every model change)
UI Responsiveness: <200ms (validated every component)
File Operation: Progress updates every 100ms
```

#### **Performance Validation Workflow**
1. **Before Implementation**: Establish baseline metrics
2. **During Development**: Continuous performance monitoring
3. **Before Commit**: Performance regression check
4. **Sprint Review**: Performance trend analysis

#### **Performance Testing Integration**
- **BenchmarkDotNet** for micro-benchmarks in unit tests
- **Memory profilers** integrated into CI pipeline
- **Load testing** for file processing workflows
- **ARM32 validation** for all performance-critical components

### **"Simple Made Easy" Validation Framework**

#### **Simplicity Gates Per Sprint**
Before proceeding to next sprint, validate:

**Architectural Simplicity Checklist:**
- [ ] **No Complecting**: Can each component be understood in isolation?
- [ ] **Composable Design**: Can components be recombined for different use cases?
- [ ] **Values Over State**: Are state changes explicit and minimal?
- [ ] **Declarative Intent**: Is the "what" clear without understanding "how"?
- [ ] **Single Responsibility**: Does each class/service have exactly one reason to change?

#### **Architectural Decision Records (ADRs)**
Document every choice that affects simplicity:

**ADR Template:**
```markdown
# ADR-XXX: [Decision Title]

## Status: [Proposed/Accepted/Deprecated]

## Context
What forces led to this decision?

## Decision
What did we decide?

## Consequences
How does this maintain/improve simplicity?
What complexity does this introduce?
How does this align with "Simple Made Easy"?
```

**Example ADRs to Create:**
- ADR-001: Why minimal APIs over controllers?
- ADR-002: Why vertical slices over layered architecture?
- ADR-003: Why SQLite over PostgreSQL for this use case?
- ADR-004: Why FastText over deep learning models?

### **User Feedback Integration Loop**

#### **User Validation Gates**
Each sprint includes user validation checkpoints:

**Sprint 1 → 2 Validation Gate**
**Question**: "Can I see my files being tracked?"
**Demo**: API showing scanned files with status
**Success Criteria**: User understands file states without technical knowledge
**Validation Method**: Show file list with clear status indicators
**Decision Point**: Continue to ML implementation or refine file tracking UX

**Sprint 2 → 3 Validation Gate**
**Question**: "Is the classification making sense?"
**Demo**: ML classification results with confidence scores
**Success Criteria**: User can correct wrong classifications intuitively
**Validation Method**: Present 10 classified files, measure correction accuracy
**Decision Point**: Proceed to file movement or improve classification accuracy

**Sprint 3 → 4 Validation Gate**
**Question**: "Can I safely organize my files?"
**Demo**: File movement with rollback capability
**Success Criteria**: User trusts the system won't lose data
**Validation Method**: Demonstrate rollback of file operations
**Decision Point**: Build web UI or enhance safety mechanisms

**Sprint 4 → Release Validation Gate**
**Question**: "Does this replace my manual workflow?"
**Demo**: Complete workflow from scan to organized library
**Success Criteria**: User prefers automated system to manual process
**Validation Method**: Time comparison and user satisfaction survey
**Decision Point**: Release v1.0 or iterate on user experience

---

## Sprint-Based Development Plan

### 🏃‍♂️ SPRINT 1: Foundation & Domain (Days 1-4)
**Theme**: Build simple, un-braided foundation with comprehensive testing
**Goal**: Establish solid domain with BaseEntity, repositories, and API core
**Success Criteria**: 45+ tests, clean architecture, no complected concerns

#### Sprint 1.1: Domain Foundation (Day 1 Morning, 4 hours) ✅ COMPLETED
**Focus**: Simple domain entities without complexity

**Task 1.1.1: Project Structure Setup (1 hour)** ✅ COMPLETED
- ✅ Initialize solution with 6 projects following dependency inversion
- ✅ Configure project dependencies (one-way only, no circular references)
- ✅ Setup NuGet packages per project boundary
- ✅ Establish clear separation of concerns

**Task 1.1.2: BaseEntity Implementation (1 hour)** ✅ COMPLETED
- ✅ Create BaseEntity abstract class with audit properties
- ✅ Implement simple state transitions (MarkAsModified, SoftDelete, Restore)
- ✅ Add comprehensive XML documentation
- ✅ Ensure no infrastructure dependencies

**Task 1.1.3: Core Domain Entities (1.5 hours)** ✅ COMPLETED
- ✅ TrackedFile entity with clear lifecycle states
- ✅ ProcessingLog entity for audit trail (separate concern)
- ✅ ConfigurationSetting entity for dynamic configuration
- ✅ UserPreference entity for future extensibility
- ✅ FileStatus enum with explicit state transitions

**Task 1.1.4: Result Pattern Implementation (30 minutes)** ✅ COMPLETED
- ✅ Generic Result<T> type for error handling without exceptions
- ✅ Success/Failure factory methods with clear semantics
- ✅ Extension methods for fluent operations
- ✅ Complete unit test coverage (38 tests - exceeded target)

#### Sprint 1.2: Data Layer (Day 1 Afternoon, 4 hours)
**Focus**: Simple repository pattern without over-abstraction

**Task 1.2.1: Database Context Setup (1 hour)** ✅ COMPLETED
- ✅ MediaButlerDbContext with explicit DbSet declarations
- ✅ Entity configurations using IEntityTypeConfiguration  
- ✅ Global query filters for soft delete
- ✅ Connection string management

**Task 1.2.2: Entity Configurations (1.5 hours)** ✅ COMPLETED
- ✅ BaseEntityConfiguration abstract class for common properties
- ✅ TrackedFileConfiguration with indexes and constraints  
- ✅ ProcessingLogConfiguration with foreign key relationships
- ✅ Database migration generation and validation

**Task 1.2.3: Repository Pattern Implementation (1 hour)** ✅ COMPLETED
- ✅ IRepository<T> interface with soft delete support
- ✅ Generic Repository<T> base implementation
- ✅ Specific ITrackedFileRepository interface  
- ✅ TrackedFileRepository with domain-specific queries

**Task 1.2.4: Unit of Work Pattern (30 minutes)** ✅ COMPLETED
- ✅ IUnitOfWork interface for transaction management
- ✅ UnitOfWork implementation with repository coordination
- ✅ Transaction scope handling
- ✅ Dispose pattern implementation

#### Sprint 1.3: Application Services (Day 2 Morning, 4 hours)
**Focus**: Application layer with clean boundaries

**Task 1.3.1: Service Interfaces (1 hour)** ✅ COMPLETED
- ✅ IFileService interface for file operations
- ✅ IConfigurationService interface for settings management
- ✅ IStatsService interface for monitoring data
- ✅ Clear method signatures without complected parameters

**Task 1.3.2: DTO and Response Models (1 hour)** ✅ COMPLETED
- ✅ TrackedFileResponse DTO with API-appropriate properties
- ✅ PagedResponse<T> for consistent pagination
- ✅ ConfigurationResponse models
- ✅ StatsResponse with aggregated data

**Task 1.3.3: Service Implementations (1.5 hours)** ✅ COMPLETED  
- ✅ FileService with CRUD operations and business logic (521 lines, production-ready)
- ✅ ConfigurationService with validation and type conversion (573 lines, enterprise-grade)
- ✅ StatsService with efficient aggregation queries (545 lines, ARM32 optimized)
- ✅ All services follow "Simple Made Easy" principles with Result<T> pattern
- ✅ Repository pattern integration with proper error handling
- ✅ Performance-oriented design for <300MB memory constraint

**Task 1.3.4: Dependency Injection Setup (30 minutes)** ✅ COMPLETED
- ✅ Service registration in DI container (Program.cs)
- ✅ Lifetime management (Scoped services for repository pattern)
- ✅ Repository and Unit of Work registration
- ✅ Test endpoints for service validation

**Task 1.3.5: Performance Validation (30 minutes)**
- **NEW**: Establish baseline API response times
- **NEW**: Memory usage profiling during service operations
- **NEW**: Database query performance analysis
- **NEW**: ARM32 compatibility validation

#### Sprint 1.4: API Layer (Day 2 Afternoon, 4 hours) ✅ COMPLETED
**Focus**: RESTful API with proper HTTP semantics

**Task 1.4.1: API Controllers Structure (1 hour)** ✅ COMPLETED
- ✅ HealthController for system monitoring (health, readiness, liveness endpoints)
- ✅ FilesController for file management operations (CRUD, pagination, workflow)
- ✅ ConfigController for configuration management (sections, search, export)
- ✅ StatsController for monitoring and analytics (comprehensive metrics)
- ✅ **ENHANCED**: Full controller implementation with 35+ RESTful endpoints
- ✅ **ENHANCED**: Complete request/response models with validation
- ✅ **ENHANCED**: XML documentation for Swagger generation
- ✅ **ENHANCED**: Error handling and Result pattern integration

✅ **Task 1.4.2: Controller Implementation (1.5 hours) - COMPLETED**
- ✅ RESTful endpoints with proper HTTP verbs and status codes
- ✅ Parameter validation and model binding with comprehensive Data Annotations
- ✅ Response formatting with consistent ApiResponse<T> wrapper structure
- ✅ Error handling middleware integration with GlobalExceptionMiddleware and PerformanceMiddleware
- ✅ **ENHANCED**: Fixed repository dependency injection for MediaButlerDbContext
- ✅ **ENHANCED**: Performance headers with ARM32-specific memory monitoring
- ✅ **ENHANCED**: Comprehensive exception mapping with standardized error codes

✅ **Task 1.4.3: Middleware and Filters (1 hour) - COMPLETED** - **ALL REQUIREMENTS MET**

**✅ Request/Response Logging Middleware**
- ✅ Correlation ID generation and tracking (8-char unique IDs for ARM32 efficiency)
- ✅ Structured request/response logging with JSON properties
- ✅ Selective body logging based on content type (JSON/XML/text)
- ✅ Performance threshold monitoring (configurable via appsettings)

**✅ Performance Monitoring Middleware**  
- ✅ Memory usage tracking (ARM32 optimized with 300MB threshold warnings)
- ✅ Request duration monitoring with structured logging
- ✅ Automatic garbage collection triggers on high allocation (>250MB)
- ✅ Working set memory warnings for ARM32 constraints

**✅ Global Exception Handling Middleware**
- ✅ Structured error logging with full context (correlation ID, request details, stack traces)
- ✅ Standardized error response format with ApiResponse wrapper
- ✅ Environment-aware error details (full details in dev, security-conscious in prod)
- ✅ Security-conscious error handling (no sensitive data in production)

**✅ Serilog Configuration**
- ✅ Multi-sink setup: Console + File + Error-only file (all configured via appsettings.json)
- ✅ ARM32-optimized file rotation and retention (7 days general, 30 days errors)
- ✅ Structured JSON logging with enrichers (machine, process, thread, service metadata)
- ✅ Development vs Production configuration (separate appsettings files)

**✅ Testing Suite VALIDATED**
- ✅ End-to-end middleware pipeline tested and working
- ✅ Correlation ID uniqueness validation confirmed
- ✅ Performance monitoring verification successful
- ✅ Structured logging output confirmed with JSON properties

**🎯 Key Features Implemented:**
- "Simple Made Easy" Compliance: Single responsibility, no complecting, composable middleware
- ARM32 Optimization: 300MB threshold monitoring, auto-GC, limited retention, performance tuning  
- Production Readiness: Structured logging, security-conscious errors, performance monitoring
- Health checks integration: Intelligent path filtering for reduced log noise

**📁 File Structure Created:**
```
src/MediaButler.API/
├── Middleware/
│   ├── RequestResponseLoggingMiddleware.cs    # Full correlation ID + structured logging
│   ├── PerformanceMonitoringMiddleware.cs     # ARM32 optimized performance tracking  
│   └── GlobalExceptionMiddleware.cs           # Environment-aware exception handling
├── appsettings.json                          # Multi-sink Serilog configuration
├── appsettings.Production.json               # Production-optimized logging
└── Program.cs                                # Middleware pipeline registration

logs/ (auto-created)
├── mediabutler-20250905.log                 # General logs (7 days retention)
└── mediabutler-errors-20250905.log          # Error logs (30 days retention)  
```

**Task 1.4.4: API Documentation (30 minutes)** ✅ COMPLETED
- ✅ Comprehensive API documentation (28KB) in docs/api-documentation.md
- ✅ Complete endpoint documentation for all 40+ API endpoints
- ✅ Request/response schemas with validation rules and examples
- ✅ Authentication model and error handling documentation
- ✅ Integration guides with JavaScript, Kotlin, and Python examples
- ✅ Performance considerations and ARM32 optimization guidelines
- ✅ Enhanced Serilog configuration for development environment

#### Sprint 1.5: Testing Infrastructure (Day 3 Morning, 4 hours)
**Focus**: Comprehensive testing strategy following "guard rail" principle

**Task 1.5.1: Test Project Setup (30 minutes)** ✅ COMPLETED
- ✅ Unit test project with xUnit and FluentAssertions
- ✅ Integration test project with in-memory database
- ✅ Acceptance test project with WebApplicationFactory
- ✅ Test data builders and object mothers
- ✅ 8 infrastructure validation tests passing
- ✅ Complete test infrastructure with fluent builders

**Task 1.5.2: Unit Test Implementation (1.5 hours)** ✅ COMPLETED
- ✅ Domain entity tests (ConfigurationSettingTests, TrackedFileTests - 20+ tests)
- ✅ Result pattern tests (ResultTests - comprehensive success/failure scenarios)
- ✅ Service layer tests (FileServiceTests - 45+ validation and business logic tests)
- ✅ Repository interface tests (IRepositoryTests - generic repository behavior)
- ✅ Infrastructure tests (TestInfrastructureTests - test framework validation)
- ✅ 129 total unit tests implemented with comprehensive coverage
- ✅ Test builders and object mothers for clean test data creation
- ✅ Following "Simple Made Easy" principles with focused, single-responsibility tests

**Task 1.5.3: Integration Test Implementation (1 hour)** ✅ COMPLETED
- ✅ Database context tests with real database (8 tests in MediaButlerDbContextTests)
- ✅ Repository implementation tests (10 tests in TrackedFileRepositoryTests)
- ✅ Transaction behavior validation (9 tests in UnitOfWorkTests)
- ✅ Service layer integration tests (8 tests in FileServiceIntegrationTests)
- ✅ 35+ comprehensive integration tests with DatabaseFixture infrastructure
- ✅ Real SQLite database testing with proper cleanup and isolation
- ✅ BaseEntity audit trail and soft delete functionality validation
- ✅ Unit of Work pattern with multi-repository transaction coordination
- ✅ All compilation errors resolved, clean build status achieved
- ✅ Following "Simple Made Easy" principles with clear test structure

**Task 1.5.4: Test Data Management (1 hour)** ✅ COMPLETED
- ✅ Enhanced TrackedFileBuilder with batch generation and TV episode patterns
- ✅ Comprehensive TestDataSeeder with 6 pre-configured scenarios
- ✅ Advanced test isolation with IntegrationTestBase and cleanup strategies  
- ✅ PerformanceDataGenerator with ARM32-optimized batch processing
- ✅ TestDataScenario enum for consistent scenario-based testing
- ✅ DatabaseFixture enhanced with seeding and auto-cleanup capabilities
- ✅ 8 comprehensive test data management validation tests
- ✅ Memory-conscious data generation for large performance datasets
- ✅ Following "Simple Made Easy" principles with clear scenario separation

#### Sprint 1.6: Acceptance Testing (Day 3 Afternoon, 4 hours)
**Focus**: End-to-end API testing and validation

**Task 1.6.1: API Test Infrastructure (1 hour)**
- WebApplicationFactory configuration
- In-memory database for testing
- HTTP client setup and authentication
- Test environment configuration

**Task 1.6.2: Endpoint Testing (2 hours)**
- Health endpoint tests (3 tests)
- Files CRUD endpoint tests (8 tests)
- Configuration endpoint tests (4 tests)
- Stats and monitoring tests (3 tests)

**Task 1.6.3: Workflow Testing (30 minutes)**
- End-to-end file processing workflow
- Error handling scenarios
- Pagination and filtering validation
- Performance under load testing

**Task 1.6.4: Performance Validation (30 minutes)**
- Memory usage testing (<300MB target)
- Response time validation (<100ms target)
- Concurrent request handling
- Database query performance

#### Sprint 1.7: Documentation & Polish (Day 4, 8 hours)
**Focus**: Documentation, code review, and final validation

**Task 1.7.1: Code Coverage Analysis (1 hour)**
- Generate coverage reports using dotnet test
- Achieve 82% line coverage target
- Identify and test uncovered critical paths
- Document coverage exclusions

**Task 1.7.2: Architecture Documentation (2 hours)**
- System architecture diagrams
- Database schema documentation
- API endpoint documentation
- Deployment guide for ARM32/Raspberry Pi

**Task 1.7.3: Development Guides (2 hours)**
- Setup instructions for new developers
- Testing strategy documentation
- Code style and conventions guide
- Troubleshooting common issues

**Task 1.7.4: Performance Benchmarking (1 hour)**
- Establish baseline performance metrics
- Memory usage profiling
- Database query optimization
- API response time analysis

**Task 1.7.5: Code Review and Refactoring (1.5 hours)**
- Review code for "complecting" anti-patterns
- Simplify overly complex implementations
- Ensure single responsibility adherence
- Validate separation of concerns

**Task 1.7.6: Final Integration Testing (30 minutes)**
- Full system integration test
- ARM32 compatibility verification
- Production configuration validation
- Final acceptance criteria verification

---

### 🏃‍♂️ SPRINT 2: ML Classification Engine (Days 5-8)
**Theme**: Intelligent filename analysis without complecting ML concerns with domain
**Goal**: Build classification engine that operates independently of core domain
**Success Criteria**: 90%+ classification accuracy, <500ms processing time

#### Sprint 2.1: ML Foundation (Day 5, 8 hours)
**Focus**: Build ML components as separate, composable units

**Task 2.1.1: ML Project Structure (1 hour)**
- Create MediaButler.ML project with clear boundaries
- Define ML-specific interfaces without domain coupling
- Setup ML.NET dependencies and configuration
- Establish training data management structure

**Task 2.1.2: Tokenization Engine (2 hours)**
- Filename parsing and token extraction
- Series name identification patterns
- Season/episode number extraction
- Quality and format recognition
- Release group identification

**Task 2.1.3: Feature Engineering (2 hours)**
- Token frequency analysis
- N-gram generation for context
- Quality indicators as features
- File size correlation features
- Regex pattern matching features

**Task 2.1.4: Training Data Collection (2 hours)**
- Sample filename dataset creation
- Manual categorization for supervised learning
- Data validation and cleaning
- Train/validation/test split strategy

**Task 2.1.5: ML Model Architecture (1 hour)**
- Multi-class classification model design
- Feature pipeline configuration
- Cross-validation strategy
- Model evaluation metrics definition

#### Sprint 2.2: Classification Implementation (Day 6, 8 hours)
**Focus**: ML model training and prediction without state complecting

**Task 2.2.1: Model Training Pipeline (2.5 hours)**
- ML.NET pipeline configuration
- Feature transformation steps
- Algorithm selection and tuning
- Training loop implementation
- Model persistence strategy

**Task 2.2.2: Prediction Service (2 hours)**
- IPredictionService interface design
- Model loading and caching strategy
- Thread-safe prediction implementation
- Confidence scoring and thresholds
- Fallback strategies for low confidence

**Task 2.2.3: Category Management (1.5 hours)**
- Dynamic category registry
- Category normalization rules
- Confidence threshold per category
- User feedback integration design
- Category suggestion ranking

**Task 2.2.4: Model Evaluation (1.5 hours)**
- Accuracy metrics calculation
- Confusion matrix analysis
- Performance benchmarking
- Cross-validation results analysis
- Model quality assertions in tests

**Task 2.2.5: ML Service Integration (30 minutes)**
- Register ML services in DI container
- Configuration management for ML parameters
- Health checks for ML model availability
- Graceful degradation when ML unavailable

#### Sprint 2.3: Background Processing (Day 7, 8 hours)
**Focus**: Asynchronous processing without blocking the API

**Task 2.3.1: Background Service Architecture (1.5 hours)**
- IHostedService implementation for file processing
- Work queue management with channels
- Cancellation token support
- Service lifetime management

**Task 2.3.2: File Processing Workflow (2.5 hours)**
- New file detection and queuing
- Hash generation and duplicate detection
- ML classification integration
- State transition management
- Error handling and retry logic

**Task 2.3.3: Processing Coordinator (2 hours)**
- Orchestrate ML classification pipeline
- Batch processing for efficiency
- Priority queue for user-requested files
- Progress tracking and reporting
- Resource throttling and backpressure

**Task 2.3.4: Integration with Domain (1.5 hours)**
- Update TrackedFile entities with ML results
- Trigger domain events for state changes
- Maintain audit trail in ProcessingLog
- Handle concurrent access scenarios

**Task 2.3.5: Monitoring and Metrics (30 minutes)**
- Processing queue metrics
- ML classification success rates
- Performance counters for throughput
- Error rate monitoring and alerting

#### Sprint 2.4: ML Testing & Validation (Day 8, 8 hours)
**Focus**: Comprehensive testing of ML pipeline with mock strategies

**Task 2.4.1: ML Unit Testing (2 hours)**
- Tokenization engine tests with varied inputs
- Feature extraction validation
- Mock ML model for consistent testing
- Classification result verification
- Error handling scenario testing

**Task 2.4.2: Integration Testing (2 hours)**
- End-to-end ML pipeline testing
- Background service integration tests
- Database integration with ML results
- Performance testing with real data
- Memory usage validation during processing

**Task 2.4.3: Model Quality Testing (2 hours)**
- Accuracy threshold validation
- Confidence score distribution analysis
- Category-specific performance testing
- False positive/negative rate analysis
- Model consistency across runs

**Task 2.4.4: Performance Testing (1.5 hours)**
- Classification speed benchmarking
- Batch processing efficiency
- Memory usage during ML operations
- Concurrent classification handling
- Resource cleanup validation

**Task 2.4.5: Documentation and Guides (30 minutes)**
- ML model training documentation
- Classification accuracy reports
- Performance benchmarks
- Troubleshooting ML-specific issues

---

### 🏃‍♂️ SPRINT 3: File Operations & Automation (Days 9-12)
**Theme**: Safe file operations with clear separation from business logic
**Goal**: Automated file organization with rollback capabilities
**Success Criteria**: Zero data loss, successful file organization, audit trail

#### Sprint 3.1: File System Monitoring (Day 9, 8 hours)
**Focus**: Watch folder monitoring as independent service

**Task 3.1.1: File Watcher Service (2 hours)**
- FileSystemWatcher implementation with proper disposal
- Event filtering for relevant file types
- Debouncing for file write completion
- Cross-platform path handling
- Service lifecycle management

**Task 3.1.2: File Discovery Pipeline (2 hours)**
- New file detection and validation
- Hash generation with streaming for large files
- Duplicate detection across watch folders
- File metadata extraction
- Integrity verification mechanisms

**Task 3.1.3: Watch Folder Management (2 hours)**
- Multiple watch folder support
- Folder configuration and validation
- Permission checking and error handling
- Recursive vs non-recursive monitoring options
- Exclude patterns and filtering rules

**Task 3.1.4: Event Processing (1.5 hours)**
- File system event queue management
- Event deduplication and batching
- Error handling for inaccessible files
- Progress reporting for large operations
- Cancellation support for long-running tasks

**Task 3.1.5: Integration Testing (30 minutes)**
- File watcher reliability testing
- Large file handling validation
- Multiple file type support
- Performance under high file volume
- Resource cleanup verification

#### Sprint 3.2: Organization Engine (Day 10, 8 hours)
**Focus**: Path generation and directory structure management

**Task 3.2.1: Path Generation Rules (2.5 hours)**
- Template-based target path generation
- Series/season/episode formatting rules
- Quality-based subfolder organization
- Special handling for movies vs TV shows
- Custom naming convention support

**Task 3.2.2: Directory Structure Management (2 hours)**
- Target directory creation with proper permissions
- Existing structure validation and repair
- Conflict resolution strategies
- Space availability checking
- Cross-platform path compatibility

**Task 3.2.3: Naming Convention Engine (2 hours)**
- Configurable naming templates
- Variable substitution system
- Validation of generated names
- Conflict resolution with existing files
- Length and character restrictions handling

**Task 3.2.4: Organization Policies (1 hour)**
- Rule-based organization decisions
- User preference integration
- Category-specific organization rules
- Quality-based folder structures
- Exception handling for edge cases

**Task 3.2.5: Preview and Validation (30 minutes)**
- Organization preview without execution
- Path validation before file operations
- Collision detection and resolution
- User confirmation workflow design
- Rollback planning for operations

#### Sprint 3.3: File Movement System (Day 11, 8 hours)
**Focus**: Safe file operations with atomic transactions

**Task 3.3.1: File Operation Engine (2.5 hours)**
- Atomic file move operations
- Copy-then-delete for cross-drive moves
- Progress tracking for large files
- Verification of successful operations
- Rollback mechanisms for failures

**Task 3.3.2: Transaction Management (2 hours)**
- File operation transaction log
- Two-phase commit for file operations
- Recovery from partial failures
- Cleanup of temporary files
- Consistency checking after operations

**Task 3.3.3: Progress Tracking (1.5 hours)**
- Real-time progress reporting
- Bandwidth throttling options
- ETA calculation for operations
- Cancellation support mid-operation
- Status persistence across restarts

**Task 3.3.4: Error Recovery (1.5 hours)**
- Comprehensive error classification
- Automatic retry with exponential backoff
- Manual intervention workflow
- Quarantine for problematic files
- Detailed error logging and reporting

**Task 3.3.5: Safety Mechanisms (30 minutes)**
- Pre-flight checks for operations
- Disk space validation
- Permission verification
- Backup verification before destructive operations
- Recovery procedures documentation

#### Sprint 3.4: Automation Testing (Day 12, 8 hours)
**Focus**: End-to-end automation validation with safety checks

**Task 3.4.1: File Operation Testing (2.5 hours)**
- File move operation reliability
- Cross-drive operation testing
- Large file handling validation
- Permission-based error scenarios
- Rollback mechanism verification

**Task 3.4.2: Integration Workflow Testing (2.5 hours)**
- Complete file discovery to organization workflow
- ML classification integration with file operations
- Error handling throughout the pipeline
- Concurrent operation handling
- System resource management

**Task 3.4.3: Performance Testing (1.5 hours)**
- Bulk file processing performance
- Memory usage during large operations
- I/O throughput optimization
- Network storage performance
- System responsiveness under load

**Task 3.4.4: Safety and Recovery Testing (1 hour)**
- Power failure simulation and recovery
- Disk full scenarios and handling
- Network interruption resilience
- Corrupted file handling
- Manual intervention workflow testing

**Task 3.4.5: Documentation and Runbooks (30 minutes)**
- File operation troubleshooting guide
- Recovery procedures documentation
- Performance tuning guidelines
- Configuration best practices

---

### 🏃‍♂️ SPRINT 4: Web Interface & User Experience (Days 13-16)
**Theme**: Simple, focused UI without complecting presentation with business logic
**Goal**: Intuitive web interface for file management and monitoring
**Success Criteria**: Responsive design, real-time updates, mobile compatibility

#### Sprint 4.1: Frontend Foundation (Day 13, 8 hours)
**Focus**: Blazor Server setup with clean component architecture

**Task 4.1.1: Blazor Server Setup (1.5 hours)**
- Project structure for MediaButler.UI
- SignalR configuration for real-time updates
- Authentication and authorization framework
- Dependency injection for UI services
- Static file handling and optimization

**Task 4.1.2: Component Architecture (2 hours)**
- Base component classes with common functionality
- State management strategy without complecting
- Event handling patterns
- Component lifecycle management
- Reusable UI component library

**Task 4.1.3: Design System (2 hours)**
- CSS framework selection and customization
- Color palette and typography
- Responsive grid system
- Icon library integration
- Dark/light theme support

**Task 4.1.4: Layout and Navigation (2 hours)**
- Main layout component structure
- Navigation menu with role-based visibility
- Breadcrumb navigation
- Mobile-responsive hamburger menu
- Loading states and progress indicators

**Task 4.1.5: Real-time Communication (30 minutes)**
- SignalR hub configuration
- Client-side connection management
- Update notification system
- Connection state handling
- Graceful degradation for connection issues

#### Sprint 4.2: Core UI Components (Day 14, 8 hours)
**Focus**: File management interface components

**Task 4.2.1: File Listing Component (2.5 hours)**
- Paginated file list with sorting
- Status-based filtering and search
- Batch selection functionality
- Context menu for file actions
- Responsive table design for mobile

**Task 4.2.2: File Details and Review (2 hours)**
- File detail modal with all metadata
- ML classification result display
- Manual category override interface
- Processing history timeline
- Image preview for media files

**Task 4.2.3: Dashboard Components (1.5 hours)**
- System status overview cards
- Processing queue visualization
- Statistics charts and graphs
- Recent activity feed
- Quick action buttons

**Task 4.2.4: Search and Filter Interface (1.5 hours)**
- Advanced search form with multiple criteria
- Saved search functionality
- Filter chips with clear indicators
- Search result highlighting
- Export search results

**Task 4.2.5: Responsive Design Testing (30 minutes)**
- Mobile device compatibility
- Tablet layout optimization
- Desktop responsiveness
- Touch interaction testing
- Accessibility validation

#### Sprint 4.3: Advanced Features (Day 15, 8 hours)
**Focus**: Configuration and bulk operations

**Task 4.3.1: Configuration Management UI (2 hours)**
- Settings categorization and organization
- Form validation with real-time feedback
- Path picker components
- Configuration export/import
- Change confirmation dialogs

**Task 4.3.2: Bulk Operations Interface (2 hours)**
- Multi-select file operations
- Bulk category assignment
- Mass file movement interface
- Progress tracking for bulk operations
- Operation cancellation support

**Task 4.3.3: Monitoring Dashboard (2 hours)**
- Real-time system metrics display
- Processing queue status visualization
- Error rate and success metrics
- Performance graphs and trends
- Alert notification system

**Task 4.3.4: User Preference Management (1.5 hours)**
- Personal settings interface
- View customization options
- Notification preferences
- Theme and layout preferences
- Data export/import for preferences

**Task 4.3.5: Help and Documentation (30 minutes)**
- In-application help system
- Tooltips and contextual help
- FAQ and troubleshooting guides
- Video tutorials integration
- Feedback and support contact

#### Sprint 4.4: UI Testing & Polish (Day 16, 8 hours)
**Focus**: Testing, accessibility, and final polish

**Task 4.4.1: Component Unit Testing (2 hours)**
- Blazor component testing with bUnit
- User interaction simulation
- State change validation
- Event handling verification
- Component isolation testing

**Task 4.4.2: Integration Testing (2 hours)**
- End-to-end user workflow testing
- SignalR communication testing
- API integration validation
- Authentication flow testing
- Error handling scenario testing

**Task 4.4.3: Accessibility and Usability (2 hours)**
- WCAG 2.1 compliance validation
- Screen reader compatibility
- Keyboard navigation testing
- Color contrast verification
- Usability testing with real users

**Task 4.4.4: Performance Optimization (1.5 hours)**
- Page load speed optimization
- Bundle size analysis and optimization
- Lazy loading implementation
- Caching strategy for static resources
- Network request optimization

**Task 4.4.5: Final Polish and Bug Fixes (30 minutes)**
- Cross-browser compatibility testing
- Final UI/UX review
- Bug fixes and edge case handling
- Performance metrics validation
- Production readiness checklist

---

## Quality Metrics & Success Criteria

### Testing Targets by Sprint
- **Sprint 1**: 45+ tests (Unit: 25, Integration: 12, Acceptance: 8)
- **Sprint 2**: 30+ additional tests (ML: 15, Integration: 10, Performance: 5)
- **Sprint 3**: 25+ additional tests (File Operations: 15, Safety: 10)
- **Sprint 4**: 20+ additional tests (UI Components: 15, E2E: 5)
- **Total**: 120+ comprehensive tests

### Performance Targets
- **Memory Usage**: <300MB under normal load
- **API Response Time**: <100ms (95th percentile)
- **ML Classification**: <500ms per file
- **File Operations**: Progress reporting every 100ms
- **UI Responsiveness**: <200ms interaction feedback

### Code Quality Gates
- **Test Coverage**: >82% line coverage maintained
- **Cyclomatic Complexity**: <10 per method average
- **Code Duplication**: <5% across solution
- **Documentation**: 100% public API documented
- **Architecture**: Zero circular dependencies

### "Simple Made Easy" Validation
- **Separation of Concerns**: Each project has single responsibility
- **No Complecting**: Domain, ML, File Operations, UI are independent
- **Composable Design**: Components can be used independently
- **Declarative Configuration**: What, not how, in settings
- **Immutable Values**: State changes are explicit and tracked

---

## Risk Management

### Technical Risks by Sprint
**Sprint 1**: Database migration complexity, Repository pattern over-abstraction
**Sprint 2**: ML model accuracy, Training data quality, Performance under load
**Sprint 3**: File system permissions, Cross-platform compatibility, Data loss scenarios
**Sprint 4**: Real-time update complexity, Mobile responsiveness, Browser compatibility

### Mitigation Strategies
- **Early Testing**: Comprehensive test coverage from day 1
- **Simple Designs**: Avoid over-engineering and complex patterns
- **Incremental Integration**: Test each layer independently before integration
- **Performance Monitoring**: Continuous performance validation
- **Rollback Capabilities**: Every operation must be reversible

---

## Success Criteria by Sprint

### Sprint 1 Success
- [x] **PROGRESS: 38+ tests implemented** (Sprint 1.1 complete - Domain Foundation with Result pattern)
- [x] **Clean architecture with no circular dependencies** (verified in Core project)
- [ ] All API endpoints functional with proper error handling
- [ ] Database operations optimized with proper indexing  
- [ ] Memory usage <300MB validated
- [ ] ARM32 compatibility confirmed

**Sprint 1 Achievements:**
- ✅ **Domain Foundation Complete** - BaseEntity, core entities, Result pattern (Sprint 1.1)
- ✅ **Data Layer Complete** - Repository pattern, Unit of Work, EF Core setup (Sprint 1.2)
- ✅ **Service Interfaces Complete** - Clean interfaces without complected parameters (Sprint 1.3.1)
- ✅ **DTO and Response Models Complete** - API-appropriate DTOs with mapping extensions (Sprint 1.3.2)
- ✅ **38 unit tests passing** (exceeded 8-test target for Result pattern)
- ✅ **Zero build warnings/errors**
- ✅ **100% XML documentation coverage**
- ✅ **"Simple Made Easy" compliance verified**

#### **Sprint 1 User Validation Gate - ENHANCED WITH OPERATIONAL VALIDATION**
**After Task 1.7.8 - Before Sprint 2**

**User Question**: "Can I see my files being tracked and understand what's happening in the system?"

**Demo Preparation**:
1. Set up test environment with sample video files
2. Configure API endpoints for file listing and status
3. Prepare demonstration of file discovery and status tracking
4. **NEW**: Set up operational monitoring dashboard showing system health
5. **NEW**: Prepare demonstration of troubleshooting capabilities

**Demo Script**:
```
1. Show watch folder with sample files
2. Trigger file scan via API
3. Display tracked files with status indicators
4. Show file metadata and processing state
5. Demonstrate filtering and search capabilities
6. NEW: Show system health dashboard with real-time metrics
7. NEW: Demonstrate error scenario and how system provides troubleshooting info
8. NEW: Show log correlation and tracing capabilities
```

**Success Criteria**:
- User understands different file states without explanation
- File information is presented clearly and logically
- User can identify which files need attention
- System status is transparent and trustworthy
- **NEW**: User feels confident they could troubleshoot issues independently
- **NEW**: System health information is actionable and understandable
- **NEW**: Performance information builds confidence in system reliability

**Validation Questions for User**:
1. "Looking at this file list, can you tell which files are ready for processing?"
2. "What would you expect to happen if you click on a file?"
3. "Does the file status make sense to you?"
4. "Are you confident the system has found all your files?"
5. **NEW**: "Looking at the system health information, do you understand if the system is working well?"
6. **NEW**: "If something went wrong, do you feel you'd have enough information to figure out what happened?"
7. **NEW**: "Does the performance information help you trust that the system will work reliably?"

**Decision Point**:
- **✅ PASS**: User comfortably navigates file tracking AND trusts operational transparency → Proceed to Sprint 2
- **❌ NEEDS WORK**: Confusion about file states OR lack of confidence in system observability → Refine UX and operational visibility before ML implementation

**Results Documentation**:
```
User Validation Results - Sprint 1:
- Date: [YYYY-MM-DD]
- File Tracking UX: [PASS/NEEDS_WORK]
- Operational Transparency: [PASS/NEEDS_WORK] 
- User Confidence Level: [High/Medium/Low]
- Troubleshooting Capability: [Sufficient/Insufficient]
- Validation Result: [PASS/NEEDS_WORK]
- User Feedback: [Key insights]
- Action Items: [Changes needed if any]
- Decision: [Proceed/Iterate]
```

### Sprint 2 Success
- [ ] 90%+ ML classification accuracy on test dataset
- [ ] <500ms classification time per file
- [ ] Background processing without blocking API
- [ ] Graceful degradation when ML unavailable
- [ ] Comprehensive ML pipeline testing

### Sprint 3 Success
- [ ] Zero data loss in file operations
- [ ] Atomic file operations with rollback
- [ ] Cross-platform file system compatibility
- [ ] Progress tracking for all operations
- [ ] Complete audit trail for file movements

### Sprint 4 Success
- [ ] Responsive design on all device sizes
- [ ] Real-time updates without page refresh
- [ ] WCAG 2.1 accessibility compliance
- [ ] <200ms UI interaction response time
- [ ] Comprehensive user workflow testing

---

*This document serves as the master development plan. Each sprint should be reviewed and updated based on learnings from the previous sprint. The "Simple Made Easy" principle should guide every architectural decision.*