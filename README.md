# CPTHub - NASM CPT Exam Preparation Application

A comprehensive Windows desktop application for NASM (National Academy of Sports Medicine) Certified Personal Trainer exam preparation, featuring AI-powered learning assistance with Google Learn LM integration.

## 🎯 Overview

CPTHub is designed to provide a complete study solution for NASM CPT certification candidates, offering:

- **AI-Powered Study Assistant** - Real-time Q&A and personalized explanations
- **Adaptive Learning System** - Content difficulty adjusts based on performance
- **Comprehensive Quiz Engine** - Practice questions with AI-generated content
- **Spaced Repetition Flashcards** - Optimized review scheduling
- **Advanced Analytics** - Detailed progress tracking and exam readiness prediction
- **Windows Integration** - Native notifications, taskbar integration, and system optimization

## 🏗️ Architecture

### Technology Stack
- **.NET 8.0** with **WPF** for native Windows performance
- **Entity Framework Core** with **SQLite** for local data storage
- **Material Design** UI framework for modern, intuitive interface
- **Google Learn LM API** for AI-powered learning features
- **MVVM Pattern** with **CommunityToolkit.Mvvm** for clean architecture

### Core Components

#### Data Layer
- **CPTHubDbContext** - Entity Framework database context
- **Models** - Complete data models for study content, user progress, and analytics
- **SQLite Database** - Local storage for offline functionality

#### Service Layer
- **AIServiceWithFallback** - Primary AI service with comprehensive error handling
- **LearnLMService** - Google Learn LM API integration
- **StudyService** - Core study functionality and progress tracking
- **QuizService** - Adaptive quiz generation and performance tracking
- **FlashcardService** - Spaced repetition algorithm implementation
- **AnalyticsService** - Comprehensive performance analytics
- **WindowsNotificationService** - Native Windows notifications
- **MemoryCacheService** - Response caching for performance
- **ErrorHandlingService** - Robust error handling and fallback mechanisms

#### Presentation Layer
- **MainWindowViewModel** - Primary application view model
- **Views** - WPF user interface components
- **Value Converters** - Data binding converters for UI

## 📚 NASM Content Structure

The application strictly follows the official NASM CPT curriculum:

### Section 1: Professional Development and Responsibility
- Chapter 1: Modern State of Health and Fitness
- Chapter 2: The Personal Training Profession

### Section 2: Client Relations and Behavioral Coaching
- Chapter 3: Psychology of Exercise
- Chapter 4: Behavioral Coaching

### Section 3: Basic and Applied Sciences and Nutritional Concepts
- Chapter 5: The Nervous, Skeletal, and Muscular Systems
- Chapter 6: The Cardiorespiratory, Endocrine, and Digestive Systems
- Chapter 7: Human Movement Science
- Chapter 8: Exercise Metabolism and Bioenergetics
- Chapter 9: Nutrition
- Chapter 10: Supplementation

### Section 4: Assessment
- Chapter 11: Health, Wellness, and Fitness Assessments
- Chapter 12: Posture, Movement, and Performance Assessments

### Section 5: Exercise Technique and Training Instruction
- Chapter 13: Integrated Training and the OPT Model
- Chapter 14: Flexibility Training Concepts
- Chapter 15: Cardiorespiratory Training
- Chapter 16: Core Training Concepts
- Chapter 17: Balance Training Concepts
- Chapter 18: Plyometric (Reactive) Training Concepts
- Chapter 19: Speed, Agility, and Quickness Training Concepts
- Chapter 20: Resistance Training Concepts

### Section 6: Program Design
- Chapter 21: The Optimum Performance Training Model
- Chapter 22: Introduction to Exercise Modalities
- Chapter 23: Chronic Health Conditions and Special Populations

## 🤖 AI Integration Features

### Google Learn LM Integration
- **Contextual Learning** - AI responses tailored to NASM content
- **Question Generation** - Dynamic quiz questions based on chapter content
- **Personalized Explanations** - Adaptive explanations based on user comprehension
- **Exam Readiness Prediction** - AI-powered assessment of preparation level

### Error Handling & Fallbacks
- **Multi-Level Fallback System** - Graceful degradation when AI is unavailable
- **Response Validation** - Cross-reference AI responses with NASM standards
- **Offline Functionality** - Core features work without internet connection
- **Rate Limit Management** - Intelligent API usage optimization

## 🎮 Core Features

### Study Modes
- **Reading Mode** - Chapter-by-chapter content with AI assistance
- **Quiz Mode** - Adaptive practice questions with instant feedback
- **Flashcard Mode** - Spaced repetition for optimal retention
- **Review Mode** - Targeted review of weak areas

### Analytics Dashboard
- **Progress Tracking** - Detailed completion and mastery metrics
- **Performance Analytics** - Chapter-by-chapter performance analysis
- **Study Streaks** - Consistency tracking and motivation
- **Exam Readiness** - AI-powered readiness assessment
- **Weak Area Identification** - Targeted improvement recommendations

### Windows Integration
- **Toast Notifications** - Study reminders and achievement alerts
- **Taskbar Integration** - Progress indicators and quick access
- **System Optimization** - Battery and performance awareness
- **Focus Assist** - Respects Windows focus modes

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- .NET 8.0 Runtime
- Internet connection for AI features (optional)

### Configuration
1. **Google Learn LM API Key** - Add your API key to `appsettings.json`:
   ```json
   {
     "GoogleLearnLM": {
       "ApiKey": "your-api-key-here"
     }
   }
   ```

2. **Privacy Settings** - Configure AI and data preferences in `appsettings.json`

### Installation
1. Clone the repository
2. Open `CPTHub.csproj` in Visual Studio
3. Restore NuGet packages
4. Configure API keys
5. Build and run

## 🔧 Development

### Project Structure
```
CPTHub/
├── Models/           # Data models
├── Services/         # Business logic services
├── ViewModels/       # MVVM view models
├── Views/           # WPF user interface
├── Data/            # Entity Framework context
├── Converters/      # Value converters
└── Assets/          # Images and resources
```

### Key Design Patterns
- **MVVM** - Clean separation of concerns
- **Dependency Injection** - Loose coupling and testability
- **Repository Pattern** - Data access abstraction
- **Circuit Breaker** - Resilient AI service calls
- **Spaced Repetition** - SM-2 algorithm for flashcards

## 📊 Analytics & Tracking

### User Progress Metrics
- **Completion Percentage** - Chapter completion tracking
- **Mastery Score** - Combined completion and quiz performance
- **Study Time** - Detailed time tracking per chapter
- **Quiz Accuracy** - Performance metrics by difficulty level
- **Retention Rate** - Flashcard review success rates

### Performance Analytics
- **Study Patterns** - Session frequency and duration analysis
- **Focus Scores** - Attention and engagement metrics
- **Streak Tracking** - Consistency and motivation metrics
- **Weak Area Detection** - AI-powered improvement recommendations

## 🔒 Privacy & Security

### Data Protection
- **Local Storage** - All user data stored locally in SQLite
- **Data Encryption** - Sensitive information encrypted at rest
- **Anonymization** - AI requests anonymized for privacy
- **User Consent** - Granular privacy controls

### AI Safety
- **Response Validation** - All AI responses validated against NASM standards
- **Content Filtering** - Inappropriate content detection and removal
- **Fallback Systems** - Graceful degradation without AI
- **Rate Limiting** - Responsible API usage

## 🎯 Exam Preparation Strategy

### Adaptive Learning Path
1. **Assessment** - Initial knowledge evaluation
2. **Personalized Plan** - AI-generated study schedule
3. **Progressive Learning** - Difficulty adjustment based on performance
4. **Targeted Review** - Focus on identified weak areas
5. **Exam Simulation** - Full-length practice exams

### Study Recommendations
- **Daily Goals** - Personalized study time recommendations
- **Chapter Sequencing** - Optimal learning order
- **Review Scheduling** - Spaced repetition optimization
- **Performance Tracking** - Continuous progress monitoring

## 🤝 Contributing

This application is designed for NASM CPT exam preparation. Contributions should maintain strict adherence to NASM standards and educational best practices.

## 📄 License

This project is for educational purposes and NASM CPT exam preparation. Please ensure compliance with NASM guidelines and copyright policies.

## 🆘 Support

For technical issues or questions about NASM content, please refer to:
- NASM official resources (nasm.org)
- Application documentation
- Error logs in the application data folder

---

**Disclaimer**: This application is designed to supplement official NASM study materials. Always refer to the official NASM CPT textbook and resources for authoritative information.
