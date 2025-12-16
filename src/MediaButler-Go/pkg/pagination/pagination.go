// Package pagination provides utilities for paginated API responses
package pagination

import "fmt"

// Request represents pagination parameters from the client
type Request struct {
	Skip int
	Take int
}

// NewRequest creates a pagination request with validation
func NewRequest(skip, take int) (*Request, error) {
	if skip < 0 {
		return nil, fmt.Errorf("skip must be >= 0, got %d", skip)
	}
	if take < 1 {
		return nil, fmt.Errorf("take must be >= 1, got %d", take)
	}
	if take > 100 {
		return nil, fmt.Errorf("take must be <= 100, got %d", take)
	}

	return &Request{
		Skip: skip,
		Take: take,
	}, nil
}

// DefaultRequest returns a default pagination request (skip=0, take=20)
func DefaultRequest() *Request {
	return &Request{Skip: 0, Take: 20}
}

// Response represents a paginated response with metadata
type Response[T any] struct {
	Items       []T  `json:"items"`
	Total       int  `json:"total"`
	Skip        int  `json:"skip"`
	Take        int  `json:"take"`
	HasNextPage bool `json:"hasNextPage"`
	HasPrevPage bool `json:"hasPreviousPage"`
}

// NewResponse creates a paginated response with calculated metadata
func NewResponse[T any](items []T, total, skip, take int) *Response[T] {
	return &Response[T]{
		Items:       items,
		Total:       total,
		Skip:        skip,
		Take:        take,
		HasNextPage: skip+take < total,
		HasPrevPage: skip > 0,
	}
}

// EmptyResponse creates an empty paginated response
func EmptyResponse[T any]() *Response[T] {
	return &Response[T]{
		Items:       make([]T, 0),
		Total:       0,
		Skip:        0,
		Take:        0,
		HasNextPage: false,
		HasPrevPage: false,
	}
}

// Query represents pagination with additional query parameters
type Query struct {
	Request
	OrderBy    string
	Descending bool
	SearchTerm string
}

// NewQuery creates a new pagination query
func NewQuery(skip, take int, orderBy string, descending bool, searchTerm string) (*Query, error) {
	req, err := NewRequest(skip, take)
	if err != nil {
		return nil, err
	}

	return &Query{
		Request:    *req,
		OrderBy:    orderBy,
		Descending: descending,
		SearchTerm: searchTerm,
	}, nil
}

// DefaultQuery returns a default query (skip=0, take=20, order by LastUpdateDate desc)
func DefaultQuery() *Query {
	return &Query{
		Request:    Request{Skip: 0, Take: 20},
		OrderBy:    "LastUpdateDate",
		Descending: true,
		SearchTerm: "",
	}
}
