# Documentation Guidelines for DataGuard Project

## Purpose
This document outlines the guidelines for maintaining comprehensive and consistent documentation across the DataGuard project. It ensures that all team members follow a standardized approach to documenting code, processes, and system architecture.

## API Documentation
API documentation should include:
- Called function
- Parameters with descriptions and types
- Required HTTP headers
- Returned parameters
- Function description
- Examples of returned values (e.g., Status: 400 Message: ...)
- Format of incoming data expected by the receiving party to avoid BadRequest

## General Guidelines
1. **Language**: All documentation must be written in Russian.
2. **Hyperlinks**: Agents are allowed to add hyperlinks to other documentation files for better navigation and context.
3. **Updates**: If an agent receives new rules in a request that are not covered in this document, it must update this document with the new rules at the end of request execution.
4. **Consistency**: Ensure that all documentation follows a consistent style and format to maintain readability and ease of use.
5. **Clarity**: Write documentation in a clear and concise manner, avoiding unnecessary jargon or complex sentences.
6. **Examples**: Provide practical examples wherever possible to illustrate concepts and usage.
7. **Versioning**: Document any changes in the API or system architecture that might affect existing implementations.

## File Structure
- **docs/client-engine-api.md**: Documentation for Client.Engine API
- **docs/server-api.md**: Documentation for Server API

## Best Practices
- **Regular Updates**: Keep documentation updated with the latest changes in the codebase.
- **Collaboration**: Encourage team collaboration in maintaining and improving documentation.
- **Feedback**: Regularly seek feedback from team members to improve the quality and usefulness of the documentation.

## Conclusion
Adhering to these guidelines will ensure that the DataGuard project documentation is comprehensive, up-to-date, and useful for all team members.