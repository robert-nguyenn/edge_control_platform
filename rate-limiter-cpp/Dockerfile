FROM ubuntu:22.04 AS build

# Install build dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    git \
    pkg-config \
    libprotobuf-dev \
    protobuf-compiler \
    protobuf-compiler-grpc \
    libgrpc++-dev \
    libgrpc-dev \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy source files
COPY . /app/

# Create build directory
RUN mkdir -p /app/build

# Build the project
WORKDIR /app/build
RUN cmake .. && \
    make -j$(nproc)

# Runtime stage
FROM ubuntu:22.04

RUN apt-get update && apt-get install -y \
    libprotobuf-dev \
    libgrpc++-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy the built executable from the build stage
COPY --from=build /app/build/rate_limiter_server /app/

# Expose gRPC port
EXPOSE 50051

# Set entry point
ENTRYPOINT ["/app/rate_limiter_server"]
