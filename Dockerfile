# Use the official .NET SDK image as a base. This includes .NET SDK and runtime.
FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy

# Set the working directory in the container
WORKDIR /app

# Install dependencies for WireGuard and libsodium (for NSec)
# Using noninteractive to avoid prompts during build
RUN apt-get update && \
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    wireguard-tools \
    libsodium23 \
    # Add any other system dependencies your library or tests might need
    && apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Note: Systemd is not explicitly installed or configured to run as PID 1 here.
# Tests involving systemctl execution against a live systemd instance will likely
# require a more specialized base image or complex Docker setup.
# For now, systemd-related tests might focus on file creation/content
# or use mocked systemctl calls if absolutely necessary for specific test logic,
# though the goal of these integration tests is to use real components.

# Copy published library and test projects (done by the run script later)
# For now, this Dockerfile just sets up the environment.
# The run_integration_tests.sh script will handle copying test files and execution.

# Default command (can be overridden by docker run)
CMD ["bash"]
