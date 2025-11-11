import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from './api.config';

export interface Transaction {
  id: number;
  account_id: number;
  transaction_upload_id: number | null;
  amount: number;
  transaction_date: string;
  direction: string;
  external_transaction_id: string | null;
}

@Injectable({ providedIn: 'root' })
export class TransactionsService {
  constructor(private http: HttpClient) {}

  getAllTransactions(): Observable<Transaction[]> {
    return this.http.get<Transaction[]>(`${API_URL}/transactions/get_all`);
  }
}