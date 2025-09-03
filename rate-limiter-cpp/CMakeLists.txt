cmake_minimum_required(VERSION 3.10)
project(edge-rate-limiter)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(Threads REQUIRED)

# Find gRPC
find_package(Protobuf REQUIRED)
find_package(gRPC CONFIG REQUIRED)

# Generate gRPC code
get_filename_component(ratelimiter_proto "ratelimiter.proto" ABSOLUTE)
get_filename_component(ratelimiter_proto_path "${ratelimiter_proto}" PATH)

set(ratelimiter_proto_srcs "${CMAKE_CURRENT_BINARY_DIR}/ratelimiter.pb.cc")
set(ratelimiter_proto_hdrs "${CMAKE_CURRENT_BINARY_DIR}/ratelimiter.pb.h")
set(ratelimiter_grpc_srcs "${CMAKE_CURRENT_BINARY_DIR}/ratelimiter.grpc.pb.cc")
set(ratelimiter_grpc_hdrs "${CMAKE_CURRENT_BINARY_DIR}/ratelimiter.grpc.pb.h")

add_custom_command(
    OUTPUT "${ratelimiter_proto_srcs}" "${ratelimiter_proto_hdrs}" "${ratelimiter_grpc_srcs}" "${ratelimiter_grpc_hdrs}"
    COMMAND ${_PROTOBUF_PROTOC}
    ARGS --grpc_out "${CMAKE_CURRENT_BINARY_DIR}"
         --cpp_out "${CMAKE_CURRENT_BINARY_DIR}"
         -I "${ratelimiter_proto_path}"
         --plugin=protoc-gen-grpc="${_GRPC_CPP_PLUGIN_EXECUTABLE}"
         "${ratelimiter_proto}"
    DEPENDS "${ratelimiter_proto}")

# Include generated files
include_directories("${CMAKE_CURRENT_BINARY_DIR}")

# Build server
add_executable(rate_limiter_server
  server.cc
  ${ratelimiter_proto_srcs}
  ${ratelimiter_grpc_srcs})

target_link_libraries(rate_limiter_server
  gRPC::grpc++
  gRPC::grpc++_reflection
  protobuf::libprotobuf
  Threads::Threads)
