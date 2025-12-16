package repository

import (
	"context"
	"database/sql"
	"fmt"
)

// UnitOfWork represents a database transaction boundary
// Implements the Unit of Work pattern for managing transactions
type UnitOfWork interface {
	// Begin starts a new transaction
	Begin(ctx context.Context) (*Transaction, error)
}

// Transaction represents an active database transaction
type Transaction struct {
	tx    *sql.Tx
	files FileRepository
}

// Commit commits the transaction
func (t *Transaction) Commit() error {
	if err := t.tx.Commit(); err != nil {
		return fmt.Errorf("commit transaction: %w", err)
	}
	return nil
}

// Rollback rolls back the transaction
func (t *Transaction) Rollback() error {
	if err := t.tx.Rollback(); err != nil {
		// Ignore "transaction already closed" errors
		if err != sql.ErrTxDone {
			return fmt.Errorf("rollback transaction: %w", err)
		}
	}
	return nil
}

// Files returns the file repository within this transaction
func (t *Transaction) Files() FileRepository {
	return t.files
}

// unitOfWork implements UnitOfWork
type unitOfWork struct {
	db *sql.DB
}

// NewUnitOfWork creates a new UnitOfWork instance
func NewUnitOfWork(db *sql.DB) UnitOfWork {
	return &unitOfWork{db: db}
}

// Begin starts a new transaction
func (u *unitOfWork) Begin(ctx context.Context) (*Transaction, error) {
	tx, err := u.db.BeginTx(ctx, nil)
	if err != nil {
		return nil, fmt.Errorf("begin transaction: %w", err)
	}

	return &Transaction{
		tx:    tx,
		files: NewFileRepositoryWithTx(tx),
	}, nil
}

// WithTransaction executes a function within a transaction
// Automatically commits on success, rolls back on error
func WithTransaction(ctx context.Context, uow UnitOfWork, fn func(*Transaction) error) error {
	tx, err := uow.Begin(ctx)
	if err != nil {
		return fmt.Errorf("begin transaction: %w", err)
	}

	defer func() {
		if err := recover(); err != nil {
			_ = tx.Rollback()
			panic(err)
		}
	}()

	if err := fn(tx); err != nil {
		_ = tx.Rollback()
		return err
	}

	if err := tx.Commit(); err != nil {
		return fmt.Errorf("commit transaction: %w", err)
	}

	return nil
}

// SavepointTransaction extends Transaction with savepoint support
// Allows partial rollbacks within a transaction
type SavepointTransaction struct {
	*Transaction
	savepoints []string
}

// CreateSavepoint creates a named savepoint
func (st *SavepointTransaction) CreateSavepoint(ctx context.Context, name string) error {
	query := fmt.Sprintf("SAVEPOINT %s", name)
	if _, err := st.tx.ExecContext(ctx, query); err != nil {
		return fmt.Errorf("create savepoint %s: %w", name, err)
	}

	st.savepoints = append(st.savepoints, name)
	return nil
}

// RollbackToSavepoint rolls back to a named savepoint
func (st *SavepointTransaction) RollbackToSavepoint(ctx context.Context, name string) error {
	query := fmt.Sprintf("ROLLBACK TO SAVEPOINT %s", name)
	if _, err := st.tx.ExecContext(ctx, query); err != nil {
		return fmt.Errorf("rollback to savepoint %s: %w", name, err)
	}

	return nil
}

// ReleaseSavepoint releases a savepoint (marks it for garbage collection)
func (st *SavepointTransaction) ReleaseSavepoint(ctx context.Context, name string) error {
	query := fmt.Sprintf("RELEASE SAVEPOINT %s", name)
	if _, err := st.tx.ExecContext(ctx, query); err != nil {
		return fmt.Errorf("release savepoint %s: %w", name, err)
	}

	// Remove from savepoints list
	for i, sp := range st.savepoints {
		if sp == name {
			st.savepoints = append(st.savepoints[:i], st.savepoints[i+1:]...)
			break
		}
	}

	return nil
}

// BeginWithSavepoints starts a transaction with savepoint support
func BeginWithSavepoints(ctx context.Context, uow UnitOfWork) (*SavepointTransaction, error) {
	tx, err := uow.Begin(ctx)
	if err != nil {
		return nil, err
	}

	return &SavepointTransaction{
		Transaction: tx,
		savepoints:  make([]string, 0),
	}, nil
}
