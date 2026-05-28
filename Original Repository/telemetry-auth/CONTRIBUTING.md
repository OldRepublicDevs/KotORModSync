# Contributing to KOTORModSync Telemetry Auth

Thank you for considering contributing to this project! This service is part of the KOTORModSync ecosystem and helps secure telemetry data.

## Getting Started

### Prerequisites

- Docker (for testing)
- Python 3.11+ (for local development)
- OpenSSL (for generating test secrets)

### Local Development Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/YOUR_ORG/kotormodsync-telemetry-auth.git
   cd kotormodsync-telemetry-auth
   ```

2. **Create a development secret:**
   ```bash
   openssl rand -hex 32 > signing_secret.txt
   ```

3. **Run locally with Docker:**
   ```bash
   docker compose up
   ```

4. **Or run with Python directly:**
   ```bash
   export KOTORMODSYNC_SIGNING_SECRET="your-dev-secret-here"
   python3 auth_service.py
   ```

5. **Test the service:**
   ```bash
   ./scripts/test-auth.sh valid
   ```

## Development Workflow

### Making Changes

1. Create a new branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. Make your changes to `auth_service.py` or documentation

3. Test your changes:
   ```bash
   # Syntax check
   python3 -m py_compile auth_service.py
   
   # Docker build test
   docker build -t test-auth .
   
   # Run integration tests
   ./scripts/test-auth.sh all
   ```

4. Commit your changes:
   ```bash
   git add .
   git commit -m "feat: add new feature"
   ```

5. Push and create a pull request:
   ```bash
   git push origin feature/your-feature-name
   ```

### Commit Message Guidelines

We follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` - New features
- `fix:` - Bug fixes
- `docs:` - Documentation changes
- `refactor:` - Code refactoring
- `test:` - Adding or updating tests
- `chore:` - Maintenance tasks

Examples:
```
feat: add support for multiple signing secrets
fix: handle malformed timestamp headers gracefully
docs: update deployment instructions
```

## Testing

### Unit Tests (Future)

```bash
python3 -m pytest tests/
```

### Integration Tests

```bash
./scripts/test-auth.sh all
```

### Docker Tests

```bash
docker compose up -d
./scripts/test-auth.sh valid
./scripts/test-auth.sh invalid
./scripts/test-auth.sh missing
docker compose down
```

## Code Style

- Follow PEP 8 style guidelines
- Use meaningful variable names
- Add docstrings to functions
- Keep functions focused and small
- Comment complex logic

### Example:

```python
def validate_signature(self, signature: str, message: str) -> bool:
    """
    Validates HMAC-SHA256 signature using constant-time comparison.
    
    Args:
        signature: Hex-encoded signature from client
        message: Message that was signed
        
    Returns:
        True if signature is valid, False otherwise
    """
    expected = hmac.new(
        self._secret.encode('utf-8'),
        message.encode('utf-8'),
        hashlib.sha256
    ).hexdigest()
    
    return hmac.compare_digest(signature.lower(), expected.lower())
```

## Security Considerations

When contributing, please:

- **Never commit secrets** - Check `.gitignore` is working
- **Use constant-time comparisons** - For signature validation
- **Validate all inputs** - Don't trust client data
- **Log security events** - Auth failures, unusual patterns
- **Follow principle of least privilege** - Run as non-root

## Documentation

When adding features:

1. Update `README.md` with usage examples
2. Update `DEPLOYMENT.md` with deployment considerations
3. Add inline code comments for complex logic
4. Update API documentation if endpoints change

## Pull Request Process

1. **Update documentation** - Ensure README reflects changes
2. **Add tests** - New features should have tests
3. **Check CI passes** - All GitHub Actions must pass
4. **Request review** - Tag a maintainer
5. **Address feedback** - Make requested changes
6. **Squash commits** - Keep history clean (optional)

### PR Checklist

- [ ] Code follows style guidelines
- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] Commit messages follow convention
- [ ] No secrets committed
- [ ] CI passes

## Reporting Issues

### Bug Reports

Include:
- Steps to reproduce
- Expected behavior
- Actual behavior
- Logs/error messages
- Environment (Docker version, OS, etc.)

### Security Issues

**DO NOT** open public issues for security vulnerabilities.

Email security concerns to: security@bolabaden.org

## Feature Requests

Open an issue with:
- Clear description of feature
- Use case / motivation
- Example implementation (if applicable)

## Questions?

- **GitHub Issues** - For bugs and features
- **GitHub Discussions** - For questions and ideas
- **Documentation** - Check README and deployment docs first

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

## Code of Conduct

Be respectful, inclusive, and professional. We're all here to make better software.

---

Thank you for contributing! 🎉

