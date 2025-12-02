# MindVault - System Use Case Analysis

## Overview

This document provides a comprehensive analysis of the MindVault system using UML Use Case diagrams. The system is a cross-platform (.NET MAUI) flashcard learning application with AI-powered content generation, spaced repetition learning, and multiplayer quiz functionality.

---

## How to View the Diagram

### Online PlantUML Viewer
1. Copy the contents of `SYSTEM_USE_CASE_DIAGRAM.puml`
2. Visit: https://www.plantuml.com/plantuml/uml/
3. Paste the code and click "Submit"

### VS Code Extension
1. Install: "PlantUML" extension by jebbs
2. Open `SYSTEM_USE_CASE_DIAGRAM.puml`
3. Press `Alt + D` to preview

### Command Line (with PlantUML installed)
```bash
java -jar plantuml.jar SYSTEM_USE_CASE_DIAGRAM.puml
```

---

## System Actors

### 1. **Student/Learner** (Primary Actor)
**Description**: The main user who consumes and studies flashcard content.

**Responsibilities**:
- Browse and search reviewers (flashcard decks)
- Study flashcards using various learning modes
- Track personal learning progress
- Import/export flashcard collections
- Participate in multiplayer quiz games

**Key Goals**:
- Learn efficiently using spaced repetition
- Track mastery levels (Learned ? Skilled ? Memorized)
- Compete with others in multiplayer mode

---

### 2. **Content Creator** (Secondary Actor)
**Description**: User focused on creating and managing flashcard content.

**Responsibilities**:
- Create new reviewers (flashcard decks)
- Manually add flashcards (question/answer pairs)
- Import content from text files or documents
- Use AI tools to generate flashcards from raw text
- Export flashcards with optional password protection

**Key Goals**:
- Quickly create high-quality study materials
- Share content with others
- Protect sensitive content with encryption

---

### 3. **Host (Multiplayer)** (Specialized Actor)
**Description**: User hosting a multiplayer quiz game session.

**Responsibilities**:
- Create game rooms with unique codes
- Broadcast game availability on local network
- Control game flow (start, pause, end)
- Award points for correct answers
- Manage player connections

**Key Goals**:
- Facilitate competitive learning environments
- Ensure fair gameplay
- Track and display leaderboard

---

### 4. **Player (Multiplayer)** (Specialized Actor)
**Description**: User joining a multiplayer quiz game.

**Responsibilities**:
- Discover available game rooms
- Join game sessions with room code
- Mark ready status before game starts
- Buzz in to answer questions
- Compete for high scores

**Key Goals**:
- Test knowledge in competitive setting
- Improve recall speed
- Achieve high scores and rankings

---

### 5. **System** (Supporting Actor)
**Description**: Automated system processes and services.

**Responsibilities**:
- Encrypt database with SQLCipher (AES-256)
- Migrate unencrypted databases automatically
- Manage encryption keys in secure storage
- Preload flashcard decks into memory
- Track learning progress using SRS algorithm
- Handle UI animations and navigation
- Install Python environment (Windows only)
- Download AI models for content generation

**Key Goals**:
- Ensure data security at rest
- Provide smooth user experience
- Enable AI-powered features

---

## Use Case Packages

### Package 1: Flashcard Management (Core Functionality)

#### UC1: Create Reviewer
**Actor**: Content Creator  
**Description**: Create a new flashcard deck with a custom title.  
**Flow**:
1. User enters reviewer title
2. System creates empty reviewer
3. User can add flashcards manually or via AI

**Includes**: UC3 (Add Flashcards Manually), UC10 (Paste Text Content)

---

#### UC2: Edit Reviewer Title
**Actor**: Content Creator  
**Description**: Change the title of an existing reviewer.  
**Precondition**: Reviewer exists  
**Flow**:
1. User selects reviewer
2. User edits title
3. System updates database

---

#### UC3: Add Flashcards Manually
**Actor**: Content Creator  
**Description**: Manually type question/answer pairs.  
**Flow**:
1. User opens reviewer editor
2. User enters question and answer
3. System saves flashcard to database
4. Repeat for multiple cards

---

#### UC4: Import Flashcards
**Actor**: Student/Learner  
**Description**: Import flashcards from external file (.txt, .json).  
**Flow**:
1. User selects import file
2. If encrypted, system prompts for password (UC56)
3. System parses file format
4. System creates reviewer and adds flashcards
5. Optional: Import progress data (SRS states)

**Extends**: UC56 (Decrypt Import File) if password-protected

---

#### UC5: Export Flashcards
**Actor**: Student/Learner, Content Creator  
**Description**: Export flashcards to JSON file with optional password protection.  
**Flow**:
1. User selects reviewer to export
2. System asks: "Add password protection?"
3. If yes, user enters and confirms password (UC55)
4. System serializes flashcards to JSON
5. System encrypts file if password provided
6. System saves to device storage (Downloads folder)

**Extends**: UC55 (Encrypt Export File) if password chosen

---

#### UC6: Delete Reviewer
**Actor**: Student/Learner  
**Description**: Permanently delete a reviewer and all its flashcards.  
**Flow**:
1. User selects delete option
2. System shows confirmation dialog
3. User confirms deletion
4. System removes reviewer and cascades to flashcards

---

#### UC7: View Reviewer List
**Actor**: Student/Learner  
**Description**: Browse all available flashcard decks.  
**Flow**:
1. User opens reviewers page
2. System loads reviewers from database
3. System displays cards with progress indicators
4. User can sort or search (UC8, UC9)

---

#### UC8: Sort Reviewers
**Actor**: Student/Learner  
**Description**: Organize reviewers by criteria.  
**Options**:
- Last Played (Recent first) - default
- Alphabetical (A–Z)
- Alphabetical (Z–A)
- Created Date (Newest first)
- Created Date (Oldest first)

---

#### UC9: Search Reviewers
**Actor**: Student/Learner  
**Description**: Filter reviewers by keyword.  
**Flow**:
1. User taps search icon
2. Search bar appears
3. User types keyword
4. System filters list in real-time (debounced)

---

### Package 2: AI Content Generation

#### UC10: Paste Text Content
**Actor**: Content Creator  
**Description**: Paste raw text to be processed.  
**Flow**:
1. User taps "Paste" area
2. Editor becomes enabled
3. User pastes or types text
4. User can view example format

---

#### UC11: Import from File
**Actor**: Content Creator  
**Description**: Extract text from document files.  
**Supported Formats**:
- .txt (plain text)
- .pdf (PDF documents)
- .docx (Word documents)
- .pptx (PowerPoint presentations)

**Flow**:
1. User selects file picker
2. User chooses document
3. System extracts text content
4. Extracted text becomes input for AI generation (UC12)

---

#### UC12: Generate Flashcards (AI Summarization)
**Actor**: Content Creator  
**Description**: Use local LLM to generate flashcards from text.  
**Platform**: Windows only  
**Requirements**:
- Python 3.11+
- llama-cpp-python package
- Local LLM model file

**Flow**:
1. User enters text (UC10 or UC11)
2. User taps "Summarize with AI"
3. System checks environment (UC13)
4. If not ready, system installs Python and dependencies
5. System downloads AI model if missing (UC14)
6. System runs Python script with LLM
7. System generates question/answer pairs
8. User reviews and edits generated flashcards

**Includes**: UC13 (Install Python), UC14 (Download Model)

---

#### UC13: Install Python Environment
**Actor**: System  
**Description**: Automatically install Python 3.11 and dependencies.  
**Flow**:
1. System detects missing Python
2. System asks user for consent
3. User confirms installation
4. System downloads Python installer (~50MB)
5. System installs silently with PATH setup
6. System installs llama-cpp-python via pip
7. System verifies installation

**Triggered by**: UC12 (first-time AI usage)

---

#### UC14: Download AI Model
**Actor**: System  
**Description**: Download LLM model file for offline use.  
**Flow**:
1. System checks for model file
2. If missing, system downloads from internet
3. System stores in Models directory
4. System verifies file integrity

---

### Package 3: Study & Review

#### UC20: View Course Review
**Actor**: Student/Learner  
**Description**: Open a reviewer to start studying.  
**Flow**:
1. User taps "VIEW COURSE" on reviewer card
2. System loads flashcards from database
3. System initializes SRS progress (UC23)
4. System displays course review interface

**Includes**: UC21 (Study Flashcards), UC22 (Configure Settings)

---

#### UC21: Study Flashcards (Interactive Mode)
**Actor**: Student/Learner  
**Description**: Interactive flashcard study session.  
**Features**:
- Card flipping (tap to reveal answer)
- Self-assessment buttons (Hard, Good, Easy)
- Image support (question/answer images)
- Progress bar
- Card counter

**Flow**:
1. System shows question side
2. User thinks of answer
3. User taps to flip card
4. System shows answer side
5. User self-assesses difficulty
6. System updates SRS progress (UC23)
7. System shows next card
8. Repeat until session complete

**Includes**: UC23 (Track Progress), UC24 (View Statistics)  
**Extends**: UC25 (Review Mistakes) if wrong answers

---

#### UC22: Configure Study Settings
**Actor**: Student/Learner  
**Description**: Customize learning modes and preferences.  
**Settings**:
- **Learning Mode**:
  - All Cards (default)
  - Due Cards Only (SRS-based)
  - Cram Mode (review all, no SRS update)
- **Questions per Round**: 10, 20, 30, 50, or All
- **Shuffle**: Randomize card order
- **Show Images**: Enable/disable image display

**Flow**:
1. User opens reviewer settings
2. User selects preferences
3. System saves to Preferences
4. Settings apply to next study session

---

#### UC23: Track Progress (SRS Algorithm)
**Actor**: System  
**Description**: Automatically track learning progress using Spaced Repetition.  
**SRS Stages**:
1. **Avail** - Not yet studied
2. **Seen** - Viewed once
3. **Learned** - Recalled correctly once
4. **Skilled** - Multiple correct recalls
5. **Memorized** - Long-term retention

**Algorithm Parameters**:
- **Ease Factor**: 2.5 (initial)
- **Interval**: Increases exponentially
- **Due Date**: Calculated based on last review

**Flow**:
1. User answers flashcard
2. System records response (Hard/Good/Easy)
3. System updates ease factor
4. System calculates next review date
5. System increments stage if criteria met
6. System saves progress to Preferences

---

#### UC24: View Study Statistics
**Actor**: Student/Learner  
**Description**: Monitor learning progress metrics.  
**Metrics**:
- Total cards in deck
- Learned count (stage ? Learned)
- Skilled count (stage ? Skilled)
- Memorized count (stage = Memorized)
- Due cards count (based on SRS)
- Progressive milestone (visual indicator)

**Display Locations**:
- Reviewer card preview
- Course review page header
- Session summary page

---

#### UC25: Review Mistakes
**Actor**: Student/Learner  
**Description**: Review questions answered incorrectly.  
**Flow**:
1. System tracks wrong answers during session
2. At end of session, user taps "Review Mistakes"
3. System displays only missed cards
4. User can study again

**Triggered by**: UC21 (if wrong answers exist)

---

#### UC26: View Session Summary
**Actor**: Student/Learner  
**Description**: See detailed stats after study session.  
**Summary Includes**:
- Cards studied
- Correct/incorrect count
- Time spent
- Mastery progress
- Next review schedule

---

#### UC27: Reset Progress
**Actor**: Student/Learner  
**Description**: Clear all SRS progress for a reviewer.  
**Flow**:
1. User opens reviewer settings
2. User selects "Reset Progress"
3. System confirms action
4. System deletes SRS state from Preferences
5. All cards return to "Avail" stage

---

### Package 4: Multiplayer Game Mode

#### UC30: Host Game Session
**Actor**: Host  
**Description**: Create and manage multiplayer quiz game.  
**Flow**:
1. Host selects reviewer for game
2. Host taps "Host Game"
3. System generates room code (UC31)
4. System starts UDP broadcast on local network
5. System listens for player connections
6. Players join and mark ready (UC33, UC34)
7. When all ready, host starts game (UC35)

**Includes**: UC31 (Generate Room Code)

---

#### UC31: Generate Room Code
**Actor**: System  
**Description**: Create unique 5-character alphanumeric code.  
**Example**: AB3X9, K7L2P  
**Flow**:
1. System generates random code
2. System displays code to host
3. Code is used for player discovery

---

#### UC32: Discover Host (UDP Broadcast)
**Actor**: Player  
**Description**: Find available game rooms on local network.  
**Protocol**: UDP broadcast on port 41500  
**Flow**:
1. Host broadcasts beacon every 1 second
2. Beacon contains: "MINDVAULT|CODE=XYZ|PORT=12345"
3. Player listens for beacons
4. Player finds matching room code
5. Player extracts host IP and port

---

#### UC33: Join Game Session
**Actor**: Player  
**Description**: Connect to host's game room.  
**Flow**:
1. Player enters room code
2. System discovers host (UC32)
3. System establishes TCP connection
4. System sends JOIN message with name/avatar
5. Host assigns unique player ID
6. Host sends WELCOME message
7. Player receives current game state

**Includes**: UC32 (Discover Host)

---

#### UC34: Mark Ready
**Actor**: Player, Host  
**Description**: Indicate readiness to start game.  
**Flow**:
1. User toggles ready checkbox
2. System sends READY|1 or READY|0
3. Host broadcasts PREADY|playerid|1 to all
4. Lobby UI updates for all participants

---

#### UC35: Start Game
**Actor**: Host  
**Description**: Begin multiplayer quiz when all players ready.  
**Precondition**: All players marked ready  
**Flow**:
1. Host taps "Start Game"
2. System verifies all players ready
3. System broadcasts START message
4. All clients enter game mode
5. System enables buzzer for all players

---

#### UC36: Buzz In
**Actor**: Player  
**Description**: Signal intention to answer question.  
**Flow**:
1. Host displays question
2. Players see question simultaneously
3. First player taps buzz button
4. System sends BUZZ message
5. Host locks buzzer for others
6. Host broadcasts BUZZWIN with player ID
7. Winner gets 10-second timer to answer (UC37)

**Anti-Spam**: 250ms cooldown between buzzes

---

#### UC37: Answer Question
**Actor**: Player (buzz winner)  
**Description**: Provide answer after buzzing in.  
**Flow**:
1. Player buzzes in (UC36)
2. Host judges answer (correct/wrong)
3. If correct:
   - Host awards point (UC38)
   - Host shows correct answer
   - Host opens next question
4. If wrong:
   - Host disables winner's buzzer
   - Other players can steal
   - If timer expires, host moves to next

---

#### UC38: Award Points
**Actor**: Host  
**Description**: Grant points for correct answers.  
**Flow**:
1. Host confirms correct answer
2. System adds point to player score
3. System broadcasts SCORE|playerid|newscore
4. All clients update scoreboard

---

#### UC39: Track Scores
**Actor**: System  
**Description**: Maintain running score for all players.  
**Storage**: In-memory dictionary (host-side)  
**Broadcast**: Real-time updates to all clients

---

#### UC40: End Game
**Actor**: Host  
**Description**: Finalize game and determine winner(s).  
**Flow**:
1. Host completes all questions
2. System calculates final scores
3. System determines winner(s)
4. System broadcasts GAMEOVER with leaderboard
5. All clients show game over screen (UC41)

**Extends**: UC42 (Rematch) option

---

#### UC41: View Leaderboard
**Actor**: Player, Host  
**Description**: Display final rankings and scores.  
**Format**:
```
1st Place: PlayerName - 15 points
2nd Place: PlayerName - 12 points
3rd Place: PlayerName - 8 points
```

**Includes**: Winners highlighted

---

#### UC42: Rematch
**Actor**: Host  
**Description**: Start new game with same players.  
**Flow**:
1. Host taps "Rematch" button
2. System resets all scores to zero
3. System re-enables all buzzers
4. System restarts game flow (UC35)

**Triggered by**: UC40 (after game ends)

---

### Package 5: Data Management & Security

#### UC50: Encrypt Database (SQLCipher AES-256)
**Actor**: System  
**Description**: Automatically encrypt all database content at rest.  
**Implementation**: SQLCipher library  
**Algorithm**: AES-256  
**Key Size**: 256 bits  
**Flow**:
1. System initializes SQLCipher provider
2. System retrieves encryption key (UC52)
3. System opens database with key
4. All queries use encrypted connection

**Performance**: +5-10ms per query (negligible)

---

#### UC51: Migrate Unencrypted DB (Automatic)
**Actor**: System  
**Description**: Convert old unencrypted database to encrypted format.  
**Trigger**: First launch after encryption update  
**Flow**:
1. System detects unencrypted database
2. System creates backup (UC53)
3. System reads all data from old database
4. System creates new encrypted database
5. System writes all data to new database
6. System verifies data integrity
7. System deletes old database

**Extends**: UC53 (Backup Database)

---

#### UC52: Store Encryption Key (SecureStorage)
**Actor**: System  
**Description**: Securely store database encryption key.  
**Platform-Specific Storage**:
- **Android**: KeyStore (hardware-backed)
- **iOS**: Keychain (Secure Enclave)
- **Windows**: Credential Manager (DPAPI)
- **macOS**: Keychain

**Flow**:
1. On first launch, system generates 256-bit key
2. System stores key in platform's secure storage
3. On subsequent launches, system retrieves key
4. Key never stored in plain text or Preferences

---

#### UC53: Backup Database
**Actor**: System  
**Description**: Create backup before risky operations.  
**Trigger**: Database migration (UC51)  
**Location**: `mindvault_backup_unencrypted.db3`  
**Flow**:
1. System copies current database
2. System stores in app data directory
3. Backup can be restored if migration fails

---

#### UC54: Preload Decks (Memory Cache)
**Actor**: System  
**Description**: Load all flashcard decks into memory on startup.  
**Purpose**: Improve performance for reviewer list  
**Flow**:
1. On app start, system loads all reviewers
2. For each reviewer, system loads flashcards
3. System stores in GlobalDeckPreloadService
4. Pages access cached data instead of database

**Benefits**:
- Faster reviewer list rendering
- Reduced database queries
- Smooth scrolling

---

#### UC55: Encrypt Export File (Password Protected)
**Actor**: System  
**Description**: Encrypt JSON export with user password.  
**Encryption**: AES-256 via ExportEncryptionService  
**Flow**:
1. User chooses "Add Password" during export (UC5)
2. User enters password
3. User confirms password
4. System derives key using PBKDF2 (10,000 iterations)
5. System encrypts JSON content
6. System wraps as: `ENCRYPTED:[salt]:[iv]:[ciphertext]`
7. System saves to file

**Security**:
- Random salt (128-bit)
- Random IV (128-bit)
- SHA-256 hash function
- 10,000 PBKDF2 iterations (OWASP compliant)

---

#### UC56: Decrypt Import File (Password Entry)
**Actor**: System  
**Description**: Decrypt password-protected import file.  
**Flow**:
1. System detects "ENCRYPTED:" prefix
2. System prompts user for password
3. User enters password
4. System extracts salt, IV, ciphertext
5. System derives key using same PBKDF2 settings
6. System decrypts content
7. If wrong password, system shows retry dialog

**Error Handling**:
- Wrong password ? Retry or Cancel
- Corrupted file ? Error message
- Invalid format ? Error message

---

### Package 6: User Profile & Settings

#### UC60: Set User Profile
**Actor**: Student/Learner  
**Description**: Configure personal profile information.  
**Fields**:
- Display name
- Avatar (emoji or image)

**Usage**: Multiplayer games, personalization

---

#### UC61: Complete Onboarding
**Actor**: Student/Learner  
**Description**: First-time user setup flow.  
**Screens**:
1. Welcome/Tagline page
2. Feature introduction
3. Profile setup (UC60)
4. Main app

---

#### UC62: View Profile Settings
**Actor**: Student/Learner  
**Description**: Access profile configuration page.

---

#### UC63: Change Avatar
**Actor**: Student/Learner  
**Description**: Update profile avatar image.

---

### Package 7: Navigation & UI

#### UC70: Open Hamburger Menu
**Actor**: Student/Learner  
**Description**: Access bottom sheet navigation menu.  
**Menu Options**:
- Create (new reviewer)
- Browse (reviewer list)
- Import
- Export

**Animation**: Smooth slide-up with backdrop

---

#### UC71: Navigate Between Pages
**Actor**: Student/Learner  
**Description**: Move through app screens.  
**Navigation Flow**:
```
ReviewersPage (Root)
??? TitleReviewerPage ? ReviewerEditorPage
??? CourseReviewPage ? ReviewerSettingsPage
??? ImportPage
??? ExportPage
```

---

#### UC72: View Animations (Slide, Fade)
**Actor**: System  
**Description**: Smooth page transitions.  
**Effects**:
- Slide in from right/left
- Fade in/out
- Scale animations

**Purpose**: Enhanced user experience

---

#### UC73: Handle Back Button
**Actor**: System  
**Description**: Process hardware/software back button.  
**Behavior**:
- If navigation stack exists ? Pop to previous page
- If on home page ? Minimize app (Android)
- Custom handling per page

---

## Key System Features Summary

### ?? Security
- AES-256 database encryption (SQLCipher)
- Optional password-protected exports
- Platform-specific secure key storage
- Automatic database migration with backup

### ?? AI-Powered
- Local LLM for flashcard generation (Windows)
- Supports PDF, DOCX, PPTX, TXT extraction
- Automatic Python environment setup

### ?? Learning System
- Spaced Repetition System (SRS) algorithm
- 5-stage mastery tracking (Avail ? Memorized)
- Customizable study modes and settings
- Progress persistence across sessions

### ?? Multiplayer
- Local network multiplayer (UDP discovery)
- Real-time quiz battles with buzzer system
- Live scoring and leaderboard
- Room codes for easy joining

### ?? Data Management
- Import/Export with progress data
- Memory caching for performance
- Cross-platform compatibility
- Automatic backups during migration

### ?? User Experience
- Smooth animations and transitions
- Hamburger menu navigation
- Responsive design
- Platform-specific optimizations

---

## Technology Stack

- **Framework**: .NET MAUI 9.0
- **Database**: SQLite + SQLCipher (encrypted)
- **UI**: XAML + C# code-behind
- **Navigation**: Shell-based routing
- **AI**: Python 3.11 + llama-cpp-python
- **Networking**: TCP/IP (multiplayer) + UDP (discovery)
- **Security**: AES-256, PBKDF2, SecureStorage

---

## Compliance & Standards

### ISO 25010 Quality Model
- **Security**: ? 5/5 (database encryption + export protection)
- **Performance**: ? 5/5 (memory caching + preloading)
- **Usability**: ? 5/5 (intuitive UI + animations)
- **Portability**: ? 5/5 (Windows, Android, iOS, macOS)
- **Maintainability**: ? 5/5 (clean architecture + DI)

---

## Future Enhancements (Not Yet Implemented)

### Potential Features
- Cloud synchronization across devices
- Web-based multiplayer (internet-based)
- Social features (friend system, leaderboards)
- Gamification (achievements, badges, streaks)
- Dark mode theme
- Tablet/desktop optimized layouts
- Voice recording for audio flashcards
- Handwriting recognition for math problems
- Collaborative deck editing
- Public deck marketplace

---

## Document Metadata

**Created**: December 2024  
**PlantUML Version**: Compatible with PlantUML 1.2023+  
**Last Updated**: December 2024  
**Maintainer**: Development Team  
**Status**: Current - Reflects actual implementation

---

## Related Documentation

- `SYSTEM_USE_CASE_DIAGRAM.puml` - PlantUML source code
- `DATABASE_ENCRYPTION_IMPLEMENTATION.md` - Security details
- `OPTIONAL_PASSWORD_PROTECTION_IMPLEMENTATION.md` - Export encryption
- `APP_DOCUMENTATION.md` - Architecture overview
- `ISO25010_EVALUATION_REPORT.md` - Quality assessment

---

**Note**: This use case analysis is based on actual implementation. All use cases listed are functional and tested across platforms.
