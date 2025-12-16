// Package result provides a Result<T> pattern for explicit error handling
// Follows the "Simple Made Easy" principle by making success/failure explicit
package result

import "fmt"

// Result represents the outcome of an operation that can succeed or fail
// This pattern replaces exceptions for control flow with explicit error handling
type Result[T any] struct {
	value T
	err   error
}

// Success creates a successful Result with a value
func Success[T any](value T) Result[T] {
	return Result[T]{value: value, err: nil}
}

// Failure creates a failed Result with an error
func Failure[T any](err error) Result[T] {
	var zero T
	return Result[T]{value: zero, err: err}
}

// FailureMsg creates a failed Result with an error message
func FailureMsg[T any](message string) Result[T] {
	return Failure[T](fmt.Errorf("%s", message))
}

// IsSuccess returns true if the Result represents a successful operation
func (r Result[T]) IsSuccess() bool {
	return r.err == nil
}

// IsFailure returns true if the Result represents a failed operation
func (r Result[T]) IsFailure() bool {
	return r.err != nil
}

// Value returns the value if successful, panics if failed
// Use IsSuccess() to check before calling Value()
func (r Result[T]) Value() T {
	if r.IsFailure() {
		panic(fmt.Sprintf("attempted to get value from failed Result: %v", r.err))
	}
	return r.value
}

// Error returns the error if failed, nil if successful
func (r Result[T]) Error() error {
	return r.err
}

// ValueOr returns the value if successful, otherwise returns the provided default
func (r Result[T]) ValueOr(defaultValue T) T {
	if r.IsSuccess() {
		return r.value
	}
	return defaultValue
}

// Map transforms the value of a successful Result
// If the Result is a failure, returns a new failure with the same error
func Map[T, U any](r Result[T], fn func(T) U) Result[U] {
	if r.IsFailure() {
		return Failure[U](r.err)
	}
	return Success(fn(r.value))
}

// FlatMap transforms a successful Result into another Result
// Useful for chaining operations that can fail
func FlatMap[T, U any](r Result[T], fn func(T) Result[U]) Result[U] {
	if r.IsFailure() {
		return Failure[U](r.err)
	}
	return fn(r.value)
}

// Unwrap returns the value and error as a tuple
// Useful for converting to Go's standard error handling
func (r Result[T]) Unwrap() (T, error) {
	return r.value, r.err
}

// UnwrapOr returns the value if successful, otherwise panics with the error
func (r Result[T]) UnwrapOr(message string) T {
	if r.IsFailure() {
		panic(fmt.Sprintf("%s: %v", message, r.err))
	}
	return r.value
}
